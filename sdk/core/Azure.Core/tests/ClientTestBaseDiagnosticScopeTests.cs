﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Azure.Core.Pipeline;
using Azure.Core.Testing;
using NUnit.Framework;

namespace Azure.Core.Tests
{
    public class ClientTestBaseDiagnosticScopeTests : ClientTestBase
    {
        public ClientTestBaseDiagnosticScopeTests(bool isAsync)
            : base(isAsync)
        {
            TestDiagnostics = true;
        }

        [Test]
        public void ThrowsWhenNoDiagnosticScope()
        {
            InvalidDiagnosticScopeTestClient client = InstrumentClient(new InvalidDiagnosticScopeTestClient());
            InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await client.NoScopeAsync());
            StringAssert.Contains("Expected some diagnostic event to fire", ex.Message);
        }

        [Test]
        public void ThrowsWhenWrongDiagnosticScope()
        {
            InvalidDiagnosticScopeTestClient client = InstrumentClient(new InvalidDiagnosticScopeTestClient());
            InvalidOperationException ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await client.WrongScopeAsync());
            StringAssert.Contains($"{typeof(InvalidDiagnosticScopeTestClient).FullName}.{nameof(client.WrongScope)}", ex.Message);

            // Make the error message more helpful
            StringAssert.Contains("ForwardsClientCalls", ex.Message);
            StringAssert.Contains("operationId", ex.Message);
            StringAssert.Contains(nameof(client.WrongScope), ex.Message);
        }

        [Test]
        public async Task DoesNotThrowForForwardedDiagnosticScope()
        {
            InvalidDiagnosticScopeTestClient client = InstrumentClient(new InvalidDiagnosticScopeTestClient());
            await client.ForwardsAsync();
        }

        [Test]
        public async Task DoesNotThrowForCorrectDiagnosticScope()
        {
            InvalidDiagnosticScopeTestClient client = InstrumentClient(new InvalidDiagnosticScopeTestClient());
            await client.CorrectScopeAsync();
        }

        public class InvalidDiagnosticScopeTestClient
        {
            private void FireScope(string method)
            {
                ClientDiagnostics clientDiagnostics = new ClientDiagnostics(true);
                string activityName = $"{typeof(InvalidDiagnosticScopeTestClient).FullName}.{method}";
                DiagnosticScope scope = clientDiagnostics.CreateScope(activityName);
                scope.Start();
                scope.Dispose();
            }

            [ForwardsClientCalls]
            public virtual Task<bool> NoScopeAsync()
            {
                return Task.FromResult(true);
            }

            [ForwardsClientCalls]
            public virtual bool NoScope()
            {
                return true;
            }

            public virtual Task<bool> WrongScopeAsync()
            {
                FireScope("DoesNotExist");
                return Task.FromResult(true);
            }

            public virtual bool WrongScope()
            {
                FireScope("DoesNotExist");
                return true;
            }

            public virtual Task<bool> CorrectScopeAsync()
            {
                FireScope(nameof(CorrectScope));
                return Task.FromResult(true);
            }

            public virtual bool CorrectScope()
            {
                FireScope(nameof(CorrectScope));
                return true;
            }

            [ForwardsClientCalls]
            public virtual Task<bool> ForwardsAsync()
            {
                FireScope(nameof(CorrectScope));
                return Task.FromResult(true);
            }

            [ForwardsClientCalls]
            public virtual bool Forwards()
            {
                FireScope(nameof(CorrectScope));
                return true;
            }
        }
    }
}
