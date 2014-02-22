﻿using Buddy;
using Espera.Core.Settings;
using ReactiveUI;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading.Tasks;

namespace Espera.Core
{
    public class Analytics : IEnableLogger
    {
        private static readonly Lazy<Analytics> instance;
        private readonly BehaviorSubject<bool> isAuthenticating;
        private BuddyClient client;
        private bool isAuthenticated;
        private AuthenticatedUser user;

        static Analytics()
        {
            instance = new Lazy<Analytics>(() => new Analytics());
        }

        public Analytics()
        {
            this.isAuthenticating = new BehaviorSubject<bool>(false);
        }

        public static Analytics Instance
        {
            get { return instance.Value; }
        }

        public async Task InitializeAsync(CoreSettings settings)
        {
            this.isAuthenticating.OnNext(true);

            this.client = new BuddyClient("Espera", "EC60C045-B432-44A6-A4E0-15B4BF607105");

            try
            {
                if (settings.AnalyticsToken == null)
                {
                    string throwAwayToken = Guid.NewGuid().ToString(); // A token that we immediately throw away because we don't need it
                    this.user = await this.client.CreateUserAsync(throwAwayToken, throwAwayToken);
                    settings.AnalyticsToken = this.user.Token;
                }

                else
                {
                    this.user = await this.client.LoginAsync(settings.AnalyticsToken);
                }
            }

            // Don't care which exception is thrown, if something bad happens the analytics are unusable
            catch (Exception ex)
            {
                this.Log().ErrorException("Couldn't login to the analytics server", ex);
                this.isAuthenticating.OnNext(false);
                return;
            }

            this.isAuthenticated = true;
            this.isAuthenticating.OnNext(false);
        }

        public async Task<bool> RecordBugReportAsync(string message)
        {
            await this.AwaitAuthenticationAsync();

            if (!this.isAuthenticated)
                return false;

            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            await this.client.Device.RecordCrashAsync(message, Environment.OSVersion.VersionString, "Desktop", this.user, null, version);

            return true;
        }

        public async Task<bool> RecordCrashAsync(Exception exception)
        {
            await this.AwaitAuthenticationAsync();

            if (!this.isAuthenticated)
                return false;

            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            await this.client.Device.RecordCrashAsync(exception.Message, Environment.OSVersion.VersionString, "Desktop", this.user, exception.StackTrace, version);

            return true;
        }

        private async Task AwaitAuthenticationAsync()
        {
            if (this.isAuthenticated)
                return;

            var finished = this.isAuthenticating.FirstAsync(x => !x).PublishLast();

            using (finished.Connect())
            {
                await finished;
            }
        }
    }
}