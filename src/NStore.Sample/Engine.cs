﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NStore.Aggregates;
using NStore.InMemory;
using NStore.Raw;
using NStore.Sample.Domain.Room;
using NStore.Sample.Projections;
using NStore.Sample.Support;
using NStore.Streams;

namespace NStore.Sample
{
    public class SampleApp : IDisposable
    {
        private readonly IRawStore _raw;

        private readonly IStreamStore _streams;
        private readonly IAggregateFactory _aggregateFactory;
        private readonly IReporter _reporter = new ColoredConsoleReporter();

        private CancellationTokenSource _source;
        private AppProjections _appProjections;

		public SampleApp()
        {
            _raw = new InMemoryRawStore(new LatencySimulator(200));
            _streams = new StreamStore(_raw);
            _aggregateFactory = new DefaultAggregateFactory();
            _appProjections = new AppProjections(_reporter);

            Subscribe();
        }

        private IRepository GetRepository()
        {
            return new Repository(_aggregateFactory, _streams);
        }

        public void CreateRooms()
        {
            var batch = Enumerable.Range(1, 10).Select( async i =>
            {
                var repository = GetRepository(); // repository is not thread safe!
                var id = "Room_" + i;
                var room = await repository.GetById<Room>(id);

                room.MakeAvailable();
                room.AddBooking(new DateRange(DateTime.Today, DateTime.Today.AddDays(5)));

                await repository.Save(room, id + "_create").ConfigureAwait(false);

                _reporter.Report("engine", $"Listed Room {id}");
            }).ToArray();

            Task.WaitAll(batch);
        }

        private void Subscribe()
        {
            _source = new CancellationTokenSource();
            var token = _source.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    await this._raw.ScanStoreAsync(
                        _appProjections.Position + 1,
                        ScanDirection.Forward,
                        _appProjections,
                        cancellationToken: token
                    );
                }
                await Task.Delay(50);
            });
        }

        public void Dispose()
        {
            _source.Cancel();
        }

		public void ShowRooms()
		{
            _reporter.Report("engine", "Rooms:");
            foreach(var r in _appProjections.Rooms.List)
            {
                _reporter.Report("engine", $"  room => {r.Id}");
            }
        }
	}
}