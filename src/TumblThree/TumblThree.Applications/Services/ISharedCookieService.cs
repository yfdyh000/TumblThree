using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace TumblThree.Applications.Services
{
    public interface ISharedCookieService
    {
        IEnumerable<Cookie> GetAllCookies(CookieContainer cookieContainer);

        void GetUriCookie(CookieContainer cookieContainer, CookieContainer request, Uri uri);

        void SetUriCookie(CookieContainer cookieContainer, IEnumerable cookies);

        void RemoveUriCookie(CookieContainer cookieContainer, Uri uri);
    }
}
