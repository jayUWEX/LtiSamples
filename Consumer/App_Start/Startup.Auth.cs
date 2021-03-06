﻿using System.Data.Entity;
using Consumer.Models;
using LtiLibrary.Core.Common;
using LtiLibrary.Core.OAuth;
using LtiLibrary.Owin.Security.Lti;
using LtiLibrary.Owin.Security.Lti.Provider;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;
using System;

namespace Consumer
{
    public partial class Startup
    {
        // For more information on configuring authentication, please visit http://go.microsoft.com/fwlink/?LinkId=301864
        public void ConfigureAuth(IAppBuilder app)
        {
            // Configure the db context, user manager and signin manager to use a single instance per request
            app.CreatePerOwinContext(ConsumerContext.Create);
            app.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);
            app.CreatePerOwinContext<ApplicationSignInManager>(ApplicationSignInManager.Create);
            // The app also uses a RoleManager
            app.CreatePerOwinContext<ApplicationRoleManager>(ApplicationRoleManager.Create);

            // Enable the application to use a cookie to store information for the signed in user
            // and to use a cookie to temporarily store information about a user logging in with a third party login provider
            // Configure the sign in cookie
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                CookieName = ".Consumer.AspNet.Cookies",
                LoginPath = new PathString("/Account/Login"),
                Provider = new CookieAuthenticationProvider
                {
                    // Enables the application to validate the security stamp when the user logs in.
                    // This is a security feature which is used when you change a password or add an external login to your account.  
                    OnValidateIdentity = SecurityStampValidator.OnValidateIdentity<ApplicationUserManager, ApplicationUser>(
                        validateInterval: TimeSpan.FromMinutes(30),
                        regenerateIdentity: (manager, user) => user.GenerateUserIdentityAsync(manager))
                }
            });
            app.UseExternalSignInCookie(DefaultAuthenticationTypes.ExternalCookie);

            // Enables the application to temporarily store user information when they are verifying the second factor in the two-factor authentication process.
            app.UseTwoFactorSignInCookie(DefaultAuthenticationTypes.TwoFactorCookie, TimeSpan.FromMinutes(5));

            // Enables the application to remember the second login verification factor such as phone or email.
            // Once you check this option, your second step of verification during the login process will be remembered on the device where you logged in from.
            // This is similar to the RememberMe option when you log in.
            app.UseTwoFactorRememberBrowserCookie(DefaultAuthenticationTypes.TwoFactorRememberBrowserCookie);

            // Uncomment the following lines to enable logging in with third party login providers
            //app.UseMicrosoftAccountAuthentication(
            //    clientId: "",
            //    clientSecret: "");

            //app.UseTwitterAuthentication(
            //   consumerKey: "",
            //   consumerSecret: "");

            //app.UseFacebookAuthentication(
            //   appId: "",
            //   appSecret: "");

            //app.UseGoogleAuthentication(new GoogleOAuth2AuthenticationOptions()
            //{
            //    ClientId = "",
            //    ClientSecret = ""
            //});

            app.UseLtiAuthentication(new LtiAuthenticationOptions
            {
                Provider = new LtiAuthenticationProvider
                {
                    // Look up the secret for the consumer
                    OnAuthenticate = async context =>
                    {
                        // Make sure the request is not being replayed
                        var timeout = TimeSpan.FromMinutes(5);
                        var oauthTimestampAbsolute = OAuthConstants.Epoch.AddSeconds(context.LtiRequest.Timestamp);
                        if (DateTime.UtcNow - oauthTimestampAbsolute > timeout)
                        {
                            throw new LtiException("Expired " + OAuthConstants.TimestampParameter);
                        }

                        var db = context.OwinContext.Get<ConsumerContext>();
                        var consumer = await db.ContentItemTools.SingleOrDefaultAsync(c => c.ConsumerKey == context.LtiRequest.ConsumerKey);
                        if (consumer == null)
                        {
                            throw new LtiException("Invalid " + OAuthConstants.ConsumerKeyParameter);
                        }

                        var signature = context.LtiRequest.GenerateSignature(consumer.ConsumerSecret);
                        if (!signature.Equals(context.LtiRequest.Signature))
                        {
                            throw new LtiException("Invalid " + OAuthConstants.SignatureParameter);
                        }

                        // If we made it this far the request is valid
                    },
                },

                // Default application signin
                SignInAsAuthenticationType = DefaultAuthenticationTypes.ApplicationCookie
            });

        }
    }
}