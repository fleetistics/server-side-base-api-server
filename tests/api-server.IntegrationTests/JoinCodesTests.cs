using api_server.Controllers.Join;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>The one-time join codes (JoinCodes) and the brake on guessing them (JoinAttemptThrottle): no database.</summary>
public sealed class JoinCodesTests
{
    private static JoinCodes Codes(string key = "test-key-0123456789") =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = key }).Build());

    [Fact]
    public void A_generated_code_has_two_groups_of_four_symbols_from_the_alphabet()
    {
        var codes = Codes();
        for (var i = 0; i < 200; i++)
        {
            var code = codes.Generate();
            code.Length.ShouldBe(9);
            code[4].ShouldBe('-');
            code.Replace("-", "").ShouldAllBe(c => "ABCDEFGHJKMNPQRSTUVWXYZ23456789".Contains(c));
        }
    }

    [Theory]
    [InlineData("k7qm-4xpd", "K7QM4XPD")]
    [InlineData("  K7QM 4XPD ", "K7QM4XPD")]
    [InlineData("K7QM4XPD", "K7QM4XPD")]
    public void Typed_codes_are_reduced_to_letters_and_digits_in_upper_case(string typed, string expected) =>
        JoinCodes.Normalize(typed).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("K7QM-4XP")]
    [InlineData("K7QM-4XPDX")]
    public void A_typed_text_of_the_wrong_length_is_not_a_code(string? typed) => JoinCodes.Normalize(typed).ShouldBeNull();

    [Fact]
    public void The_hash_is_stable_per_code_and_differs_between_codes_and_keys()
    {
        var a = Codes();
        a.Hash("K7QM4XPD").ShouldBe(a.Hash("K7QM4XPD"));
        a.Hash("K7QM4XPD").ShouldNotBe(a.Hash("K7QM4XPE"));
        a.Hash("K7QM4XPD").ShouldNotBe(Codes("another-key-0123456789").Hash("K7QM4XPD"));
        a.Hash("K7QM4XPD").ShouldNotContain("K7QM");
        a.Hash("K7QM4XPD").Length.ShouldBe(64);
    }

    [Fact]
    public void A_user_is_locked_after_too_many_wrong_codes_and_unlocked_by_a_right_one()
    {
        var time = new FakeTime(DateTimeOffset.Parse("2026-11-24T10:00:00Z"));
        var throttle = new JoinAttemptThrottle(Options.Create(new JoinOptions { MaxFailedAttempts = 3, LockoutMinutes = 10 }), time);

        for (var i = 0; i < 2; i++) throttle.RecordFailure(7);
        throttle.IsLocked(7).ShouldBeFalse();
        throttle.RecordFailure(7);
        throttle.IsLocked(7).ShouldBeTrue();
        throttle.IsLocked(8).ShouldBeFalse();

        time.Now = time.Now.AddMinutes(11);
        throttle.IsLocked(7).ShouldBeFalse();

        throttle.RecordFailure(7);
        throttle.Reset(7);
        throttle.RecordFailure(7);
        throttle.RecordFailure(7);
        throttle.IsLocked(7).ShouldBeFalse();
    }

    private sealed class FakeTime(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
