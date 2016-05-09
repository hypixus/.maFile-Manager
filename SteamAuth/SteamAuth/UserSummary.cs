﻿using System.Linq;
using System.Net;
using Newtonsoft.Json;
using UnofficialSteamAuthenticator.Lib.Models;

namespace UnofficialSteamAuthenticator.Lib.SteamAuth
{
    /// <summary>
    ///     Class to help align system time with the Steam server time. Not super advanced; probably not taking some things
    ///     into account that it should.
    ///     Necessary to generate up-to-date codes. In general, this will have an error of less than a second, assuming Steam
    ///     is operational.
    /// </summary>
    public class UserSummary
    {
        public static void GetSummaries(SteamWeb web, SessionData session, ulong[] steamids, SummaryCallback summariesCallback)
        {
            web.Request(ApiEndpoints.USER_SUMMARIES_URL + "?access_token=" + session.OAuthToken + "&steamids=" + string.Join(",", steamids.Select(steamid => steamid.ToString()).ToArray()), "GET", (response, code) =>
            {
                summariesCallback(code != HttpStatusCode.OK ? new Players() : JsonConvert.DeserializeObject<Players>(response ?? string.Empty));
            });
        }
    }
}
