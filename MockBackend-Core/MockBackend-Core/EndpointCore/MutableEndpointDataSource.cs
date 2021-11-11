using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MockBackend_Core.EndpointCore
{
    public abstract class MutableEndpointDataSource : EndpointDataSource
    {
        private readonly object _lock = new object();

        private IReadOnlyList<Endpoint> _endpoints = new List<Endpoint>();

        private CancellationTokenSource? _cancellationTokenSource;

        private IChangeToken? _changeToken;

        public MutableEndpointDataSource() : this(Array.Empty<Endpoint>()) { }

        public MutableEndpointDataSource(IReadOnlyList<Endpoint> endpoints)
        {
            SetEndpoints(endpoints);
        }

#pragma warning disable CS8764 // Nullability of return type doesn't match overridden member (possibly because of nullability attributes).
        public override IChangeToken? GetChangeToken() => _changeToken;
#pragma warning restore CS8764 // Nullability of return type doesn't match overridden member (possibly because of nullability attributes).

        public override IReadOnlyList<Endpoint> Endpoints => _endpoints;

        public void SetEndpoints(IReadOnlyList<Endpoint> endpoints)
        {
            lock (_lock)
            {
                var oldCancellationTokenSource = _cancellationTokenSource;

                _endpoints = endpoints;

                _cancellationTokenSource = new CancellationTokenSource();
                _changeToken = new CancellationChangeToken(_cancellationTokenSource.Token);

                oldCancellationTokenSource?.Cancel();
            }
        }
    }
}
