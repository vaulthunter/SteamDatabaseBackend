﻿/*
 * Copyright (c) 2013-2018, SteamDB. All rights reserved.
 * Use of this source code is governed by a BSD-style license that can be
 * found in the LICENSE file.
 */

using System;
using System.Timers;
using SteamKit2;

namespace SteamDatabaseBackend
{
    class SteamAnonymous
    {
        private readonly Timer ReconnectionTimer;

        public SteamClient Client { get; }
        private SteamUser User { get; }
        public SteamApps Apps { get; }
        private CallbackManager CallbackManager { get; }

        public bool IsRunning { get; set; }

        public SteamAnonymous()
        {
            ReconnectionTimer = new Timer
            {
                AutoReset = false
            };
            ReconnectionTimer.Elapsed += Reconnect;
            ReconnectionTimer.Interval = TimeSpan.FromSeconds(Connection.RETRY_DELAY).TotalMilliseconds;
            
            Client = new SteamClient(Steam.Configuration);
            User = Client.GetHandler<SteamUser>();
            Apps = Client.GetHandler<SteamApps>();

            CallbackManager = new CallbackManager(Client);

            CallbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            CallbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            CallbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);

            IsRunning = true;
        }

        public void Tick()
        {
            Client.Connect();

            while (IsRunning)
            {
                CallbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(5));
            }
        }

        private void Reconnect(object sender, ElapsedEventArgs e)
        {
            if (Client.IsConnected)
            {
                Log.WriteDebug("SteamAnonymous", "Reconnect timer fired, but it's connected already.");

                return;
            }

            Log.WriteDebug("SteamAnonymous", "Reconnecting...");

            Client.Connect();
        }

        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            Log.WriteInfo("SteamAnonymous", "Connected, logging in to cellid {0}...", LocalConfig.CellID);

            User.LogOnAnonymous(new SteamUser.AnonymousLogOnDetails
            {
                CellID = LocalConfig.CellID
            });
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            if (!IsRunning)
            {
                Log.WriteInfo("SteamAnonymous", "Disconnected from Steam");

                return;
            }

            Log.WriteInfo("SteamAnonymous", "Disconnected from Steam. Retrying in {0} seconds...", Connection.RETRY_DELAY);

            ReconnectionTimer.Start();
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                Log.WriteInfo("SteamAnonymous", "Failed to login: {0}", callback.Result);

                return;
            }

            Log.WriteInfo("SteamAnonymous", "Logged in, current Valve time is {0}", callback.ServerTime.ToString("R"));
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Log.WriteInfo("SteamAnonymous", "Logged out of Steam: {0}", callback.Result);
        }
    }
}
