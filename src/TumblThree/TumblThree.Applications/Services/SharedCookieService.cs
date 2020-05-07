﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net;
using System.Net.Http;
using System.Reflection;

namespace TumblThree.Applications.Services
{
    [Export(typeof(ISharedCookieService))]
    [Export]
    public class SharedCookieService : ISharedCookieService
    {
        //private readonly CookieContainer cookieContainer = new CookieContainer(); // TODO
        
        public void GetUriCookie(CookieContainer cookieContainer, CookieContainer request, Uri uri)
        {
            foreach (Cookie cookie in cookieContainer.GetCookies(uri))
            {
                request.Add(cookie);
            }
        }

        public void SetUriCookie(CookieContainer cookieContainer, IEnumerable cookies)
        {
            foreach (Cookie cookie in cookies)
            {
                cookieContainer.Add(cookie);
            }
        }

        public void RemoveUriCookie(CookieContainer cookieContainer, Uri uri)
        {
            CookieCollection cookies = cookieContainer.GetCookies(uri);
            foreach (Cookie cookie in cookies)
            {
                cookie.Expired = true;
            }
        }

        public IEnumerable<Cookie> GetAllCookies(CookieContainer cookieContainer)
        {
            var k = (Hashtable)cookieContainer
                                     .GetType().GetField("m_domainTable", BindingFlags.Instance | BindingFlags.NonPublic)
                                     .GetValue(cookieContainer);
            foreach (DictionaryEntry element in k)
            {
                var l = (SortedList)element.Value.GetType()
                                                  .GetField("m_list", BindingFlags.Instance | BindingFlags.NonPublic)
                                                  .GetValue(element.Value);
                foreach (object e in l)
                {
                    var cl = (CookieCollection)((DictionaryEntry)e).Value;
                    foreach (Cookie fc in cl)
                    {
                        yield return fc;
                    }
                }
            }
        }
    }
}
