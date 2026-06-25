using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace OrderManagement.IntegrationTests.Infrastructure
{
    [CollectionDefinition("Integration")]
    public class IntegrationCollection : ICollectionFixture<IntegrationTestFixture>
    {
    }
}
