using RaceDay.Mvc.Models;

namespace RaceDay.Mvc.Auth;

/// <summary>Values kept in the website's session while a user is signed in.</summary>
public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string FullName = "FullName";
    public const string ProfileImageUrl = "ProfileImageUrl";

    /// <summary>The API's own session cookie ("name=value"), forwarded on every API call for this user.</summary>
    public const string ApiCookie = "ApiCookie";
}

/// <summary>Reads the signed-in user out of the session. Used by controllers and the layout.</summary>
public static class SessionUserExtensions
{
    public static bool IsSignedIn(this ISession session) =>
        session.GetInt32(SessionKeys.UserId) is not null && session.GetUserRole() is not null;

    public static int? GetUserId(this ISession session) => session.GetInt32(SessionKeys.UserId);

    public static UserRole? GetUserRole(this ISession session) =>
        Enum.TryParse<UserRole>(session.GetString(SessionKeys.Role), out var role) ? role : null;

    public static string GetFullName(this ISession session) => session.GetString(SessionKeys.FullName) ?? string.Empty;

    public static bool IsOrganiser(this ISession session) => session.GetUserRole() == UserRole.Organiser;

    public static bool IsParticipant(this ISession session) => session.GetUserRole() == UserRole.Participant;

    public static void SignIn(this ISession session, UserDto user)
    {
        session.SetInt32(SessionKeys.UserId, user.UserId);
        session.SetString(SessionKeys.Role, user.Role.ToString());
        session.SetString(SessionKeys.FullName, user.FullName);
        session.SetString(SessionKeys.ProfileImageUrl, user.ProfileImageUrl ?? string.Empty);
    }
}
