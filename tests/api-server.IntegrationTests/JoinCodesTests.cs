using api_server.Controllers.Join;
using Microsoft.Extensions.Options;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>The join codes (JoinCodes) and the brake on guessing them (JoinAttemptThrottle): no database.</summary>
public sealed class JoinCodesTests
{
    private static JoinCodes Codes() => new();

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

    [Theory]
    [InlineData("k7qm-4xpd", "K7QM-4XPD")]
    [InlineData("  K7QM 4XPD ", "K7QM-4XPD")]
    [InlineData("K7QM4XPD", "K7QM-4XPD")]
    public void A_typed_code_is_formatted_the_way_it_is_stored(string typed, string expected) => JoinCodes.Format(typed).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("K7QM")]
    public void A_text_that_is_not_a_code_has_no_stored_form(string? typed) => JoinCodes.Format(typed).ShouldBeNull();

    [Fact]
    public void A_generated_code_is_already_in_the_stored_form()
    {
        var code = Codes().Generate();
        JoinCodes.Format(code).ShouldBe(code);
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
