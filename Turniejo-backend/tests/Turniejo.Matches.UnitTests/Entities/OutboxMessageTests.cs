using FluentAssertions;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Matches.UnitTests.Entities
{
    // OutboxMessage żyje w SharedKernel (współdzielony przez wszystkie moduły), ale nie ma tam
    // własnego projektu testowego — testujemy go tutaj, tak jak inne encje domenowe w tym projekcie.
    public sealed class OutboxMessageTests
    {
        [Fact]
        public void MarkAsFailed_ShouldIncrementRetryCountAndSetError()
        {
            var message = OutboxMessage.Create("SomeEvent", "{}");

            message.MarkAsFailed("boom");

            message.RetryCount.Should().Be(1);
            message.Error.Should().Be("boom");
        }

        [Fact]
        public void MarkAsFailed_ShouldScheduleNextAttemptInTheFuture_WhenRetriesRemain()
        {
            // Zamyka lukę z audytu: bez backoffu OutboxProcessor uderzał w tę samą wiadomość co każdy
            // cykl (10s), niezależnie od tego, ile razy już zawiodła.
            var message = OutboxMessage.Create("SomeEvent", "{}");
            var before = DateTimeOffset.UtcNow;

            message.MarkAsFailed("boom");

            message.NextAttemptAt.Should().NotBeNull();
            message.NextAttemptAt!.Value.Should().BeAfter(before);
        }

        [Fact]
        public void MarkAsFailed_ShouldIncreaseBackoffDelay_WithEachSubsequentFailure()
        {
            var message = OutboxMessage.Create("SomeEvent", "{}");

            message.MarkAsFailed("boom-1");
            var firstDelay = message.NextAttemptAt!.Value;

            message.MarkAsFailed("boom-2");
            var secondDelay = message.NextAttemptAt!.Value;

            secondDelay.Should().BeAfter(firstDelay);
        }

        [Fact]
        public void MarkAsFailed_ShouldDeadLetterAndClearNextAttempt_WhenMaxRetryCountReached()
        {
            // Zamyka drugą część luki: po wyczerpaniu prób wiadomość musi przestać być w ogóle
            // podejmowana (NextAttemptAt == null wypada z filtra GetPendingAsync), a nie cicho
            // czekać w nieskończoność.
            var message = OutboxMessage.Create("SomeEvent", "{}");

            for (var i = 0; i < OutboxMessage.MaxRetryCount; i++)
                message.MarkAsFailed($"attempt-{i}");

            message.RetryCount.Should().Be(OutboxMessage.MaxRetryCount);
            message.IsDeadLettered.Should().BeTrue();
            message.NextAttemptAt.Should().BeNull();
        }

        [Fact]
        public void MarkAsFailed_ShouldNotBeDeadLettered_BeforeMaxRetryCountReached()
        {
            var message = OutboxMessage.Create("SomeEvent", "{}");

            for (var i = 0; i < OutboxMessage.MaxRetryCount - 1; i++)
                message.MarkAsFailed($"attempt-{i}");

            message.IsDeadLettered.Should().BeFalse();
            message.NextAttemptAt.Should().NotBeNull();
        }

        [Fact]
        public void IsDeadLettered_ShouldBeFalse_WhenProcessedEvenAfterManyFailures()
        {
            var message = OutboxMessage.Create("SomeEvent", "{}");

            for (var i = 0; i < OutboxMessage.MaxRetryCount; i++)
                message.MarkAsFailed($"attempt-{i}");
            message.MarkAsProcessed();

            message.IsDeadLettered.Should().BeFalse();
        }
    }
}
