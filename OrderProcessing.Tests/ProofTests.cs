using Xunit;

namespace OrderProcessing.Tests;

public class ProofTests
{
    [Fact(Skip = "Run manually against dockerized app and save output to tests/concurrency-proof.txt")]
    public void ConcurrencyProof()
    {
    }

    [Fact(Skip = "Run manually against dockerized app and save output to tests/idempotency-proof.txt")]
    public void IdempotencyProof()
    {
    }
}
