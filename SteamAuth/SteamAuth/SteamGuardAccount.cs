﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage.Streams;
using HtmlAgilityPack;
using Newtonsoft.Json;
using UnofficialSteamAuthenticator.Lib.Models;
using UnofficialSteamAuthenticator.Lib.Models.SteamAuth;

namespace UnofficialSteamAuthenticator.Lib.SteamAuth
{
    public class SteamGuardAccount : ISteamSecrets
    {
        private static readonly byte[] SteamGuardCodeTranslations =
        {
            50, 51, 52, 53, 54, 55, 56, 57, 66, 67, 68, 70, 71, 72, 74, 75, 77, 78, 80, 81, 82, 84, 86, 87, 88, 89
        };

        [JsonProperty("personaname")]
        private string displayName;

        [JsonProperty("notify_since")]
        private int notifySince;

        public string DisplayName
        {
            get { return this.displayName; }
            set
            {
                this.DisplayCache = DateTime.UtcNow;
                this.displayName = value;
            }
        }

        public int NotifySince
        {
            get { return this.notifySince; }
            set { this.notifySince = Math.Max(this.notifySince, value); }
        }

        [JsonProperty("shared_secret")]
        public string SharedSecret { get; set; }

        [JsonProperty("serial_number")]
        public string SerialNumber { get; set; }

        [JsonProperty("revocation_code")]
        public string RevocationCode { get; set; }

        [JsonProperty("uri")]
        public string URI { get; set; }

        [JsonProperty("server_time")]
        public long ServerTime { get; set; }

        [JsonProperty("account_name")]
        public string AccountName { get; set; }

        [JsonProperty("token_gid")]
        public string TokenGID { get; set; }

        [JsonProperty("identity_secret")]
        public string IdentitySecret { get; set; }

        [JsonProperty("secret_1")]
        public string Secret1 { get; set; }

        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("device_id")]
        public string DeviceID { get; set; }

        [JsonProperty("personacache")]
        public DateTime DisplayCache { get; private set; }

        /// <summary>
        ///     Set to true if the authenticator has actually been applied to the account.
        /// </summary>
        [JsonProperty("fully_enrolled")]
        public bool FullyEnrolled { get; set; }

        public SessionData Session { get; set; }

        public void GenerateSteamGuardCode(IWebRequest web, Callback callback)
        {
            TimeAligner.GetSteamTime(web, time =>
            {
                callback(this.GenerateSteamGuardCodeForTime(time));
            });
        }

        /// <summary>
        ///     Refreshes the Steam session. Necessary to perform confirmations if your session has expired or changed.
        /// </summary>
        /// <returns></returns>
        public void RefreshSession(SteamWeb web, BfCallback callback)
        {
            string url = ApiEndpoints.MOBILEAUTH_GETWGTOKEN;
            var postData = new Dictionary<string, string>();
            postData.Add("access_token", this.Session.OAuthToken);

            web.Request(url, "POST", postData, (response, code) =>
            {
                if (response == null || code != HttpStatusCode.OK)
                {
                    callback(Success.Error);
                    return;
                }

                try
                {
                    var refreshResponse = JsonConvert.DeserializeObject<WebResponse<RefreshSessionDataResponse>>(response);
                    if (string.IsNullOrEmpty(refreshResponse?.Response?.Token))
                    {
                        callback(Success.Failure);
                        return;
                    }

                    string token = this.Session.SteamID + "%7C%7C" + refreshResponse.Response.Token;
                    string tokenSecure = this.Session.SteamID + "%7C%7C" + refreshResponse.Response.TokenSecure;

                    this.Session.SteamLogin = token;
                    this.Session.SteamLoginSecure = tokenSecure;
                    Storage.PushStore(this.Session);

                    callback(Success.Success);
                }
                catch (Exception)
                {
                    callback(Success.Error);
                }
            });
        }

        public void PushStore()
        {
            Storage.PushStore(this);
        }

        public void DeactivateAuthenticator(SteamWeb web, int scheme, BCallback callback)
        {
            var postData = new Dictionary<string, string>();
            postData.Add("steamid", this.Session.SteamID.ToString());
            postData.Add("steamguard_scheme", scheme.ToString());
            postData.Add("revocation_code", this.RevocationCode);
            postData.Add("access_token", this.Session.OAuthToken);

            web.MobileLoginRequest(ApiEndpoints.STEAMAPI_BASE + "/ITwoFactorService/RemoveAuthenticator/v0001", "POST", postData, (res, code) =>
            {
                try
                {
                    var removeResponse = JsonConvert.DeserializeObject<WebResponse<UnlinkResponse>>(res);

                    callback(removeResponse?.Response != null && (removeResponse.Response.Success || removeResponse.Response.AttemptsRemaining > 0));
                }
                catch (Exception)
                {
                    callback(false);
                }
            });
        }

        public void DeactivateAuthenticator(SteamWeb web, BCallback callback)
        {
            // Default scheme = 2
            this.DeactivateAuthenticator(web, 2, callback);
        }

        public string GenerateSteamGuardCodeForTime(long time)
        {
            if (string.IsNullOrEmpty(this.SharedSecret))
            {
                return "";
            }

            IBuffer sharedSecretArray = CryptographicBuffer.DecodeFromBase64String(this.SharedSecret);
            var timeArray = new byte[8];

            time /= 30L;

            for (var i = 8; i > 0; i--)
            {
                timeArray[i - 1] = (byte) time;
                time >>= 8;
            }
            IBuffer data = CryptographicBuffer.CreateFromByteArray(timeArray);

            MacAlgorithmProvider hmacsha1 = MacAlgorithmProvider.OpenAlgorithm("HMAC_SHA1");
            CryptographicKey hmacKey = hmacsha1.CreateKey(sharedSecretArray);

            byte[] hashedData = CryptographicEngine.Sign(hmacKey, data).ToArray();
            var codeArray = new byte[5];
            try
            {
                var b = (byte) (hashedData[19] & 0xF);
                int codePoint = (hashedData[b] & 0x7F) << 24 | (hashedData[b + 1] & 0xFF) << 16 | (hashedData[b + 2] & 0xFF) << 8 | (hashedData[b + 3] & 0xFF);

                for (var i = 0; i < 5; ++i)
                {
                    codeArray[i] = SteamGuardCodeTranslations[codePoint % SteamGuardCodeTranslations.Length];
                    codePoint /= SteamGuardCodeTranslations.Length;
                }
            }
            catch (Exception)
            {
                return null; //Change later, catch-alls are bad!
            }
            return Encoding.UTF8.GetString(codeArray, 0, codeArray.Length);
        }

        public void FetchConfirmations(SteamWeb web, FcCallback callback)
        {
            this.GenerateConfirmationUrl(web, url =>
            {
                var cookies = new CookieContainer();
                this.Session.AddCookies(cookies);

                web.Request(url, "GET", null, cookies, (response, code) =>
                {
                    var ret = new List<Confirmation>();
                    if (response == null || code != HttpStatusCode.OK)
                    {
                        // Forbidden = family view, NotFound = bad token
                        callback(ret, code == HttpStatusCode.Forbidden ? null : new WgTokenInvalidException());
                        return;
                    }

                    // Was it so hard?
                    var doc = new HtmlDocument();
                    doc.LoadHtml(response);

                    HtmlNode list = doc.GetElementbyId("mobileconf_list");
                    if (list == null || response.Contains("<div>Nothing to confirm</div>"))
                    {
                        callback(ret, null);
                        return;
                    }

                    IEnumerable<HtmlNode> entries = list.Descendants("div").Where(child => child.Attributes["class"]?.Value == "mobileconf_list_entry");

                    foreach (HtmlNode node in entries)
                    {
                        // Look now, html is complicated
                        string[] desc = node.Descendants("div").First(child => child.Attributes["class"].Value == "mobileconf_list_entry_description").Descendants("div").Select(elem => elem.InnerText).ToArray();
                        ret.Add(new Confirmation
                        {
                            Description = desc[0],
                            Description2 = desc[1],
                            DescriptionTime = desc[2],
                            ID = int.Parse(node.GetAttributeValue("data-confid", "0")),
                            Key = node.GetAttributeValue("data-key", "")
                        });
                    }

                    callback(ret, null);
                });
            });
        }

        public void AcceptConfirmation(SteamWeb web, Confirmation conf, BCallback callback)
        {
            this._sendConfirmationAjax(web, conf, "allow", callback);
        }

        public void DenyConfirmation(SteamWeb web, Confirmation conf, BCallback callback)
        {
            this._sendConfirmationAjax(web, conf, "cancel", callback);
        }

        private void _sendConfirmationAjax(SteamWeb web, Confirmation conf, string op, BCallback callback)
        {
            this.GenerateConfirmationQueryParams(web, op, queryParams =>
            {
                string url = ApiEndpoints.COMMUNITY_BASE + "/mobileconf/ajaxop";
                string queryString = "?op=" + op + "&";
                queryString += queryParams;
                queryString += "&cid=" + conf.ID + "&ck=" + conf.Key;
                url += queryString;

                var cookies = new CookieContainer();
                this.Session.AddCookies(cookies);

                web.Request(url, "GET", null, cookies, (response, code) =>
                {
                    if (response == null || code != HttpStatusCode.OK)
                    {
                        callback(false);
                        return;
                    }

                    var confResponse = JsonConvert.DeserializeObject<SuccessResponse>(response);
                    callback(confResponse.Success);
                });
            });
        }

        public void GenerateConfirmationUrl(SteamWeb web, string tag, Callback callback)
        {
            this.GenerateConfirmationQueryParams(web, tag, queryString =>
            {
                callback(this.GenerateConfirmationUrl(queryString));
            });
        }

        public void GenerateConfirmationUrl(SteamWeb web, Callback callback)
        {
            // Default tag = conf
            this.GenerateConfirmationUrl(web, "conf", callback);
        }

        public string GenerateConfirmationUrl(string queryString)
        {
            return ApiEndpoints.COMMUNITY_BASE + "/mobileconf/conf?" + queryString;
        }

        public void GenerateConfirmationQueryParams(SteamWeb web, string tag, Callback callback)
        {
            if (string.IsNullOrEmpty(this.DeviceID))
                throw new ArgumentException("Device ID is not present");

            TimeAligner.GetSteamTime(web, time =>
            {
                callback("p=" + this.DeviceID + "&a=" + this.Session.SteamID.ToString() + "&k=" + this._generateConfirmationHashForTime(time, tag) + "&t=" + time + "&m=android&tag=" + tag);
            });
        }

        private string _generateConfirmationHashForTime(long time, string tag)
        {
            int n2 = tag != null ? Math.Min(40, 8 + tag.Length) : 8;
            var array = new byte[n2];
            for (var n4 = 7; n4 >= 0; n4--)
            {
                array[n4] = (byte) time;
                time >>= 8;
            }
            if (tag != null)
            {
                Array.Copy(Encoding.UTF8.GetBytes(tag), 0, array, 8, n2 - 8);
            }

            try
            {
                IBuffer identitySecretArray = CryptographicBuffer.DecodeFromBase64String(this.IdentitySecret);

                MacAlgorithmProvider hmacsha1 = MacAlgorithmProvider.OpenAlgorithm("HMAC_SHA1");
                CryptographicKey hmacKey = hmacsha1.CreateKey(identitySecretArray);

                string encodedData = CryptographicBuffer.EncodeToBase64String(CryptographicEngine.Sign(hmacKey, CryptographicBuffer.CreateFromByteArray(array)));
                string hash = WebUtility.UrlEncode(encodedData);
                return hash;
            }
            catch (Exception)
            {
                return null; //Fix soon: catch-all is BAD!
            }
        }

        public void GetConfirmationTradeOfferId(SteamWeb web, Confirmation conf, LongCallback callback)
        {
            this._getConfirmationDetails(web, conf, confDetails =>
            {
                if (confDetails == null || !confDetails.Success)
                {
                    callback(-1);
                    return;
                }

                var tradeOfferIdRegex = new Regex("<div class=\"tradeoffer\" id=\"tradeofferid_(\\d+)\" >");
                if (!tradeOfferIdRegex.IsMatch(confDetails.Html))
                {
                    callback(-1);
                    return;
                }
                callback(long.Parse(tradeOfferIdRegex.Match(confDetails.Html).Groups[1].Value));
            });
        }

        private void _getConfirmationDetails(SteamWeb web, Confirmation conf, ConfirmationCallback callback)
        {
            string url = ApiEndpoints.COMMUNITY_BASE + "/mobileconf/details/" + conf.ID + "?";
            this.GenerateConfirmationQueryParams(web, "details", queryString =>
            {
                url += queryString;

                var cookies = new CookieContainer();
                this.Session.AddCookies(cookies);
                this.GenerateConfirmationUrl(web, referer =>
                {
                    web.Request(url, "GET", null, cookies, (response, code) =>
                    {
                        if (string.IsNullOrEmpty(response) || code != HttpStatusCode.OK)
                        {
                            callback(null);
                            return;
                        }

                        var confResponse = JsonConvert.DeserializeObject<ConfirmationDetailsResponse>(response);
                        callback(confResponse);
                    });
                });
            });
        }
    }
}
