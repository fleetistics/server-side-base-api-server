using System.Reflection;
using api_server.Auth.Dto;
using db_model.AppStructure;
using Shouldly;

namespace api_server.IntegrationTests;

/// <summary>
/// ClientSideInfo (the wire DTO) and SessionClientInfo (the persisted complex type) are
/// copied field-by-field in AuthService.RotateSessionAsync — nothing checks that copy is
/// complete. This guards against silent drift: add a field to one side and forget the
/// other, and this test fails instead of the field just quietly never reaching the DB.
/// </summary>
public sealed class ClientSideInfoMappingTests
{
    // ClientSideInfo.PlatformName (raw string) is deliberately NOT copied verbatim: AuthService
    // converts it to SessionClientInfo.ClientDevicePlatformId (normalized lookup id) instead of
    // straight field-by-field copy — so this one pair is excluded from the parity check.
    private static readonly string[] DtoOnlyExceptions = ["PlatformName"];
    private static readonly string[] EntityOnlyExceptions = ["ClientDevicePlatformId"];

    [Fact]
    public void ClientSideInfo_And_SessionClientInfo_HaveMatchingFields()
    {
        var dtoFields = PublicPropertyNames<ClientSideInfo>();
        var entityFields = PublicPropertyNames<SessionClientInfo>();

        // Except() ignores the HashSet's own comparer and uses the default (case-sensitive)
        // one unless told otherwise — must pass it explicitly here too.
        var onlyOnDto = dtoFields.Except(entityFields, StringComparer.OrdinalIgnoreCase)
            .Except(DtoOnlyExceptions, StringComparer.OrdinalIgnoreCase).ToList();
        var onlyOnEntity = entityFields.Except(dtoFields, StringComparer.OrdinalIgnoreCase)
            .Except(EntityOnlyExceptions, StringComparer.OrdinalIgnoreCase).ToList();

        (onlyOnDto.Count == 0 && onlyOnEntity.Count == 0).ShouldBeTrue(
            $"ClientSideInfo and SessionClientInfo have drifted.\n" +
            $"  Only on ClientSideInfo: [{string.Join(", ", onlyOnDto)}]\n" +
            $"  Only on SessionClientInfo: [{string.Join(", ", onlyOnEntity)}]");
    }

    // Case-insensitive: ClientSideInfo.AppUid vs SessionClientInfo.AppUID is the one
    // deliberate casing mismatch between the two — everything else matches exactly.
    private static HashSet<string> PublicPropertyNames<T>() =>
        typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
