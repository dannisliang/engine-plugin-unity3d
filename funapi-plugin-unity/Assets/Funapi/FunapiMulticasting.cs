﻿// Copyright (C) 2013-2015 iFunFactory Inc. All Rights Reserved.
//
// This work is confidential and proprietary to iFunFactory Inc. and
// must not be used, disclosed, copied, or distributed without the prior
// consent of iFunFactory Inc.


using Fun;
using ProtoBuf;
using System.Collections.Generic;
using UnityEngine;

using funapi.network.fun_message;
using funapi.service.multicast_message;


namespace Fun
{
    public class FunapiMulticastClient
    {
        #region public interface
        public delegate void ChannelReceiveHandler(string channel_id, object body);

        public FunapiMulticastClient(FunMsgType msg_type)
        {
            msg_type_ = msg_type;
        }

        public bool Started
        {
            get
            {
                return network_ != null && network_.Started;
            }
        }

        public void Connect(string hostname_or_ip, ushort port)
        {
            bool need_to_start = false;

            Debug.Log ("Multicast server is at " + hostname_or_ip + ":" + port);

            lock (lock_)
            {
                transport_ = new FunapiTcpTransport (hostname_or_ip, port);
                DebugUtils.Assert (transport_ != null);
                network_ = new FunapiNetwork (msg_type_, false);
                network_.AttachTransport (transport_);
                network_.RegisterHandler(kMulticastMsgType, OnReceived);
                need_to_start = true;
            }

            if (need_to_start)
            {
                network_.Start ();
            }
        }

        public bool Connected
        {
            get { return network_ != null && network_.Connected; }
        }


        public bool JoinChannel(string channel_id, ChannelReceiveHandler handler)
        {
            lock (lock_)
            {
                if (network_ == null || !network_.Started)
                {
                    Debug.Log ("Not connected. First connect before join a multicast channel.");
                    return false;
                }

                if (channels_.ContainsKey(channel_id)) {
                    Debug.Log ("Already joined the channel: " + channel_id);
                    return false;
                }

                channels_.Add (channel_id, handler);
            }

            FunMulticastMessage mcast_msg = new FunMulticastMessage ();
            mcast_msg.channel = channel_id;
            mcast_msg.join = true;

            FunMessage fun_msg = new FunMessage ();
            Extensible.AppendValue (fun_msg, 8, mcast_msg);
            network_.SendMessage (kMulticastMsgType, fun_msg);

            return true;
        }

        public bool LeaveChannel(string channel_id)
        {
            lock (lock_)
            {
                if (network_ == null || !network_.Started)
                {
                    Debug.Log ("Not connected. If you are trying to leave a channel in which you were, connect first while preserving the session id you used for join.");
                    return false;
                }
                if (!channels_.ContainsKey(channel_id))
                {
                    Debug.Log ("You are not in the channel: " + channel_id);
                    return false;
                }

                channels_.Remove(channel_id);
            }

            FunMulticastMessage mcast_msg = new FunMulticastMessage ();
            mcast_msg.channel = channel_id;
            mcast_msg.leave = true;

            FunMessage fun_msg = new FunMessage ();
            Extensible.AppendValue (fun_msg, 8, mcast_msg);
            network_.SendMessage (kMulticastMsgType, fun_msg);

            return true;
        }

        public bool InChannel(string channel_id)
        {
            lock (lock_)
            {
                return channels_.ContainsKey(channel_id);
            }
        }

        /// <summary>
        /// The sender must fill in the mcast_msg.
        /// The "channel_id" field is mandatory.
        /// And mcas_msg must have join and leave flags set.
        /// </summary>
        public bool SendToChannel(FunMulticastMessage mcast_msg)
        {
            DebugUtils.Assert (msg_type_ == FunMsgType.kProtobuf);
            DebugUtils.Assert (mcast_msg != null);
            DebugUtils.Assert (!mcast_msg.join);
            DebugUtils.Assert (!mcast_msg.leave);

            string channel_id = mcast_msg.channel;
            DebugUtils.Assert (channel_id != "");

            lock (lock_)
            {
                if (network_ == null || !network_.Started)
                {
                    Debug.Log ("Not connected. If you are trying to leave a channel in which you were, connect first while preserving the session id you used for join.");
                    return false;
                }
                if (!channels_.ContainsKey(channel_id))
                {
                    Debug.Log ("You are not in the channel: " + channel_id);
                    return false;
                }
            }

            FunMessage fun_msg = new FunMessage ();
            Extensible.AppendValue (fun_msg, 8, mcast_msg);
            network_.SendMessage (kMulticastMsgType, fun_msg);
            return true;
        }

        /// <summary>
        /// The sender must fill in the mcast_msg.
        /// The "channel_id" field is mandatory.
        /// And mcas_msg must have join and leave flags set.
        /// </summary>
        public bool SendToChannel(object json_msg) {
            DebugUtils.Assert (msg_type_ == FunMsgType.kJson);
            // TODO(dkmoon): Verifies the passed json_msg has required fields.
            network_.SendMessage (kMulticastMsgType, json_msg);
            return true;
        }


        /// <summary>
        /// Please call this Update function inside your Unity3d Update.
        /// </summary>
        public void Update()
        {
            if (network_ != null)
                network_.Update ();
        }
        #endregion

        #region internal implementation
        private void OnReceived(string msg_type, object body) {
            DebugUtils.Assert (msg_type == kMulticastMsgType);
            FunMessage msg = body as FunMessage;
            DebugUtils.Assert (msg != null);
            FunMulticastMessage mcast_msg = Extensible.GetValue<FunMulticastMessage> (msg, 8);
            string channel_id = mcast_msg.channel;

            lock (lock_)
            {
                if (!channels_.ContainsKey(channel_id))
                {
                    Debug.Log("You are not in the channel: " + mcast_msg.channel);
                    return;
                }
                ChannelReceiveHandler h = channels_[channel_id];
                h(channel_id, mcast_msg);
            }
        }

        private const string kMulticastMsgType = "_multicast";

        private FunMsgType msg_type_;
        private FunapiNetwork network_;
        private FunapiTcpTransport transport_;
        private Dictionary<string, ChannelReceiveHandler> channels_ = new Dictionary<string, ChannelReceiveHandler> ();

        private object lock_ = new object();
        #endregion
    }
}
