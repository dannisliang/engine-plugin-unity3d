﻿// Copyright (C) 2013 iFunFactory Inc. All Rights Reserved.
//
// This work is confidential and proprietary to iFunFactory Inc. and
// must not be used, disclosed, copied, or distributed without the prior
// consent of iFunFactory Inc.

using Fun;
using MiniJSON;
using ProtoBuf;
using System;
using System.Collections.Generic;
using UnityEngine;

// Protobuf
using funapi.network.fun_message;
using funapi.network.maintenance;
using funapi.service.multicast_message;
using pbuf_echo;
using pbuf_multicast;


public class FunapiNetworkTester : MonoBehaviour
{
    public void Start()
    {
        announcement_url_ = string.Format("http://{0}:{1}", kAnnouncementIp, kAnnouncementPort);
        announcement_.Init(announcement_url_);
        announcement_.ResultCallback += new FunapiAnnouncement.EventHandler(OnAnnouncementResult);
    }

    public void Update()
    {
        if (network_ != null)
            network_.Update();

        if (multicast_ != null)
            multicast_.Update ();

        if (chat_ != null)
            chat_.Update ();
    }

    public void OnGUI()
    {
        //----------------------------------------------------------------------------
        // FunapiNetwork test
        //----------------------------------------------------------------------------
        with_protobuf_ = GUI.Toggle(new Rect(30, 0, 300, 20), with_protobuf_, " google protocol buffer");
        with_session_reliability_ = GUI.Toggle(new Rect(30, 20, 300, 20), with_session_reliability_, " session reliability");
        GUI.Label(new Rect(30, 40, 300, 20), "server : " + kServerIp);

        GUI.enabled = (network_ == null || !network_.Started);
        if (GUI.Button(new Rect(30, 60, 240, 40), "Connect (TCP)"))
        {
            Connect(TransportProtocol.kTcp);
        }
        if (GUI.Button(new Rect(30, 110, 240, 40), "Connect (UDP)"))
        {
            Connect(TransportProtocol.kUdp);
        }
        if (GUI.Button(new Rect(30, 160, 240, 40), "Connect (HTTP)"))
        {
            Connect(TransportProtocol.kHttp);
        }

        GUI.enabled = (network_ != null && network_.Connected);
        if (GUI.Button(new Rect(30, 210, 240, 40), "Disconnect"))
        {
            DisConnect();
        }

        if (GUI.Button(new Rect(30, 260, 240, 40), "Send 'Hello World'"))
        {
            SendEchoMessage();
        }

        //----------------------------------------------------------------------------
        // Announcements test
        //----------------------------------------------------------------------------
        GUI.enabled = announcement_ != null;
        GUI.Label(new Rect(30, 320, 300, 20), "server : " + announcement_url_);
        if (GUI.Button(new Rect(30, 340, 240, 40), "Update Announcements"))
        {
            announcement_.UpdateList();
        }

        //----------------------------------------------------------------------------
        // Resource download test
        //----------------------------------------------------------------------------
        GUI.enabled = downloader_ == null;
        GUI.Label(new Rect(30, 390, 300, 20), "server : " + kDownloadServerIp + ":" + kDownloadServerPort);
        if (GUI.Button(new Rect(30, 410, 240, 40), "File Download (HTTP)"))
        {
            downloader_ = new FunapiHttpDownloader(FunapiUtils.GetLocalDataPath, OnDownloadUpdate, OnDownloadFinished);
            downloader_.StartDownload(kDownloadServerIp, kDownloadServerPort, "list", false);
            Debug.Log("Start downloading..");
            Invoke("CheckDownloadConnection", 3f);
        }

        //----------------------------------------------------------------------------
        // FunapiMulticasting test
        //----------------------------------------------------------------------------
        GUI.enabled = (multicast_ == null || !multicast_.Connected);
        GUI.Label(new Rect(280, 40, 300, 20), "server : " + kMulticastServerIp);
        string multicast_title = "Multicast (Protobuf) connect";
        if (GUI.Button(new Rect(280, 60, 240, 40), multicast_title))
        {
            if (multicast_ == null)
                multicast_ = new FunapiMulticastClient(FunMsgType.kProtobuf);

            multicast_.Connect(kMulticastServerIp, kMulticastPbufPort);
            Debug.Log("Connecting to the multicast server..");
        }

        GUI.enabled = (multicast_ != null && multicast_.Connected && !multicast_.InChannel(kMulticastTestChannel));
        multicast_title = "Multicast (Protobuf) join";
        if (GUI.Button(new Rect(280, 110, 240, 40), multicast_title))
        {
            multicast_.JoinChannel(kMulticastTestChannel, OnMulticastChannelSignalled);
            Debug.Log("Joining the multicast channel '" + kMulticastTestChannel + "'");
        }

        GUI.enabled = (multicast_ != null && multicast_.Connected && multicast_.InChannel(kMulticastTestChannel));
        multicast_title = "Multicast (Protobuf) send";
        if (GUI.Button(new Rect(280, 160, 240, 40), multicast_title))
        {
            PbufHelloMessage hello_msg = new PbufHelloMessage();
            hello_msg.message = "multicast test";

            FunMulticastMessage mcast_msg = new FunMulticastMessage();
            mcast_msg.channel = kMulticastTestChannel;
            mcast_msg.bounce = true;

            Extensible.AppendValue(mcast_msg, 8, hello_msg);

            multicast_.SendToChannel(mcast_msg);

            Debug.Log("Sending a message to the multicast channel '" + kMulticastTestChannel + "'");
        }

        GUI.enabled = (multicast_ != null && multicast_.Connected && multicast_.InChannel(kMulticastTestChannel));
        multicast_title = "Multicast (Protobuf) leave";
        if (GUI.Button(new Rect(280, 210, 240, 40), multicast_title))
        {
            multicast_.LeaveChannel(kMulticastTestChannel);
            Debug.Log("Leaving the multicast channel '" + kMulticastTestChannel + "'");
        }

        GUI.enabled = (chat_ == null || !chat_.Connected);
        string chat_title = "Chat (Protobuf) connect";
        if (GUI.Button(new Rect(280, 260, 240, 40), chat_title))
        {
            if (chat_ == null)
                chat_ = new FunapiChatClient();

            chat_.Connect(kMulticastServerIp, kMulticastPbufPort, FunMsgType.kProtobuf);
            Debug.Log("Connecting to the chat server..");
        }

        GUI.enabled = (chat_ != null && chat_.Connected && !chat_.InChannel(kChatTestChannel));
        chat_title = "Chat (Protobuf) join";
        if (GUI.Button(new Rect(280, 310, 240, 40), chat_title))
        {
            chat_.JoinChannel(kChatTestChannel, kChatUserName, OnChatChannelReceived);
            Debug.Log("Joining the chat channel '" + kChatTestChannel + "'");
        }

        GUI.enabled = (chat_ != null && chat_.Connected && chat_.InChannel(kChatTestChannel));
        chat_title = "Chat (Protobuf) send";
        if (GUI.Button(new Rect(280, 360, 240, 40), chat_title))
        {
            chat_.SendText(kChatTestChannel, "hello world");

            Debug.Log("Sending a message to the chat channel '" + kChatTestChannel + "'");
        }

        GUI.enabled = (chat_ != null && chat_.Connected && chat_.InChannel(kChatTestChannel));
        chat_title = "Chat (Protobuf) leave";
        if (GUI.Button(new Rect(280, 410, 240, 40), chat_title))
        {
            chat_.LeaveChannel(kChatTestChannel);
            Debug.Log("Leaving the chat channel '" + kChatTestChannel + "'");
        }
    }


    private FunapiTransport GetNewTransport (TransportProtocol protocol)
    {
        FunapiTransport transport = null;

        if (protocol == TransportProtocol.kTcp)
        {
            transport = new FunapiTcpTransport(kServerIp, (ushort)(with_protobuf_ ? 8022 : 8012));
            //transport.DisableNagle = true;
        }
        else if (protocol == TransportProtocol.kUdp)
        {
            transport = new FunapiUdpTransport(kServerIp, (ushort)(with_protobuf_ ? 8023 : 8013));

            // Please set the same encryption type as the encryption type of server.
            //transport.SetEncryption(EncryptionType.kIFunEngine2Encryption);
        }
        else if (protocol == TransportProtocol.kHttp)
        {
            FunapiHttpTransport http_transport = new FunapiHttpTransport(kServerIp, (ushort)(with_protobuf_ ? 8028 : 8018), false);
            http_transport.RequestFailureCallback += new FunapiHttpTransport.RequestFailureHandler(OnHttpRequestFailure);
            transport = http_transport;

            // Please set the same encryption type as the encryption type of server.
            //transport.SetEncryption(EncryptionType.kIFunEngine2Encryption);
        }

        if (transport != null)
        {
            transport.StoppedCallback += new StoppedEventHandler(OnTransportClosed);

            // Timeout method only works with Tcp protocol.
            transport.ConnectTimeoutCallback += new ConnectTimeoutHandler(OnConnectTimeout);
            transport.ConnectTimeout = 3f;

            // If you prefer use specific Json implementation other than Dictionary,
            // you need to register json accessors to handle the Json implementation before FunapiNetwork::Start().
            // E.g., transport.JsonHelper = new YourJsonAccessorClass
        }

        return transport;
    }

    private void Connect (TransportProtocol protocol)
    {
        Debug.Log("-------- Connect --------");

        FunMsgType msg_type = with_protobuf_ ? FunMsgType.kProtobuf : FunMsgType.kJson;
        if (network_ == null || !network_.SessionReliability || network_.MsgType != msg_type)
        {
            network_ = new FunapiNetwork(msg_type, with_session_reliability_);

            network_.OnSessionInitiated += new FunapiNetwork.SessionInitHandler(OnSessionInitiated);
            network_.OnSessionClosed += new FunapiNetwork.SessionCloseHandler(OnSessionClosed);
            network_.MaintenanceCallback += new FunapiNetwork.MessageEventHandler(OnMaintenanceMessage);
            network_.StoppedAllTransportCallback += new FunapiNetwork.NotifyHandler(OnStoppedAllTransport);

            network_.RegisterHandler("echo", this.OnEcho);
            network_.RegisterHandler("pbuf_echo", this.OnEchoWithProtobuf);

            //network_.SetMessageProtocol(TransportProtocol.kTcp, "echo");
            //network_.SetMessageProtocol(TransportProtocol.kUdp, "pbuf_echo");

            FunapiTransport transport = GetNewTransport(protocol);
            network_.AttachTransport(transport);
        }
        else
        {
            if (!network_.HasTransport(protocol))
            {
                FunapiTransport transport = GetNewTransport(protocol);
                network_.AttachTransport(transport);
            }

            network_.SetDefaultProtocol(protocol);
        }

        network_.Start();
    }

    private void DisConnect ()
    {
        CancelInvoke();

        if (network_.Started == false)
        {
            Debug.Log("You should connect first.");
        }
        else if (network_.SessionReliability)
        {
            network_.StopTransportAll();
        }
        else
        {
            network_.Stop();
        }
    }

    private void CheckConnection ()
    {
        if (network_ == null)
        {
            Debug.Log("Failed to make a connection. Network instance was not generated.");
        }
        else if (!network_.Connected)
        {
            Debug.LogWarning("Maybe the server is down? Stopping the network module.");

            network_.Stop();
        }
        else
        {
            Debug.Log("Seems network succeeded to make a connection to a server.");
        }
    }

    private void CheckDownloadConnection ()
    {
        if (downloader_ != null && !downloader_.Connected)
        {
            Debug.Log("Maybe the server is down? Stopping Download.");

            downloader_.Stop();
            downloader_ = null;
        }
    }

    private void SendEchoMessage ()
    {
        if (network_.Started == false && !network_.SessionReliability)
        {
            Debug.Log("You should connect first.");
        }
        else
        {
            if (network_.MsgType == FunMsgType.kJson)
            {
                // In this example, we are using Dictionary<string, object>.
                // But you can use your preferred Json implementation (e.g., Json.net) instead of Dictionary,
                // by changing JsonHelper member in FunapiTransport.
                // Please refer to comments inside Connect() function.
                Dictionary<string, object> message = new Dictionary<string, object>();
                message["message"] = "hello world";
                network_.SendMessage("echo", message);
            }
            else if (network_.MsgType == FunMsgType.kProtobuf)
            {
                PbufEchoMessage echo = new PbufEchoMessage();
                echo.msg = "hello proto";
                FunMessage message = network_.CreateFunMessage(echo, 16);
                network_.SendMessage("pbuf_echo", message);
            }
        }
    }

    private void OnSessionInitiated (string session_id)
    {
        Debug.Log("Session initiated. Session id:" + session_id);
    }

    private void OnSessionClosed ()
    {
        Debug.Log("Session closed.");
    }

    private void OnConnectTimeout (TransportProtocol protocol)
    {
        Debug.Log(protocol + " Transport Connection timed out.");
    }

    private void OnTransportClosed (TransportProtocol protocol)
    {
        Debug.Log(protocol + " Transport closed.");
    }

    private void OnEcho (string msg_type, object body)
    {
        DebugUtils.Assert(body is Dictionary<string, object>);
        string strJson = Json.Serialize(body as Dictionary<string, object>);
        Debug.Log("Received an echo message: " + strJson);
    }

    private void OnEchoWithProtobuf (string msg_type, object body)
    {
        DebugUtils.Assert(body is FunMessage);
        FunMessage msg = body as FunMessage;
        object obj = network_.GetMessage(msg, typeof(PbufEchoMessage), 16);
        if (obj == null)
            return;

        PbufEchoMessage echo = obj as PbufEchoMessage;
        Debug.Log("Received an echo message: " + echo.msg);
    }

    private void OnDownloadUpdate (string path, long bytes_received, long total_bytes, int percentage)
    {
        Debug.Log("Downloading - path:" + path + " / received:" + bytes_received + " / total:" + total_bytes + " / " + percentage + "%");
    }

    private void OnDownloadFinished (DownloadResult code)
    {
        downloader_ = null;
        Debug.Log("Download completed. result:" + code);
    }

    private void OnAnnouncementResult (AnnounceResult result)
    {
        Debug.Log("OnAnnouncementResult - result: " + result);
        if (result != AnnounceResult.kSuccess)
            return;

        if (announcement_.ListCount > 0)
        {
            for (int i = 0; i < announcement_.ListCount; ++i)
            {
                Dictionary<string, object> list = announcement_.GetAnnouncement(i);
                string buffer = "";

                foreach (var item in list)
                    buffer += item.Key + ": " + item.Value + "\n";

                Debug.Log("announcement >> " + buffer);
            }
        }
    }

    private void OnMaintenanceMessage (string msg_type, object body)
    {
        if (network_.MsgType == FunMsgType.kJson)
        {
            DebugUtils.Assert(body is Dictionary<string, object>);
            Dictionary<string, object> msg = body as Dictionary<string, object>;
            Debug.Log(String.Format("Maintenance message\nstart: {0}\nend: {1}\nmessage: {2}",
                                    msg["date_start"], msg["date_end"], msg["messages"]));
        }
        else if (network_.MsgType == FunMsgType.kProtobuf)
        {
            FunMessage msg = body as FunMessage;
            object obj = network_.GetMessage(msg, typeof(MaintenanceMessage), 15);
            if (obj == null)
                return;

            MaintenanceMessage maintenance = obj as MaintenanceMessage;
            Debug.Log(String.Format("Maintenance message\nstart: {0}\nend: {1}\nmessage: {2}",
                                    maintenance.date_start, maintenance.date_end, maintenance.messages));
        }
        else
        {
            DebugUtils.Assert(false);
        }
    }

    private void OnStoppedAllTransport()
    {
        Debug.Log("OnStoppedAllTransport called.");
    }

    private void OnHttpRequestFailure (string msg_type)
    {
        Debug.Log("OnHttpRequestFailure - msg_type: " + msg_type);
    }


    private void OnMulticastChannelSignalled(string channel_id, object body)
    {
        DebugUtils.Assert (body is FunMulticastMessage);
        FunMulticastMessage mcast_msg = body as FunMulticastMessage;
        DebugUtils.Assert (mcast_msg.channel == kMulticastTestChannel);
        PbufHelloMessage hello_msg = Extensible.GetValue<PbufHelloMessage>(mcast_msg, 8);
        DebugUtils.Assert (hello_msg.message != "");
        Debug.Log ("Received a multicast message from a channel " + channel_id);
    }

    private void OnChatChannelReceived(string chat_channel, string sender, string text)
    {
        Debug.Log ("Received a chat channel message. Channel=" + chat_channel + ", sender=" + sender + ", text=" + text);
    }

    // Please change this address for test.
    private const string kServerIp = "127.0.0.1";
    private const string kAnnouncementIp = "127.0.0.1";
    private const UInt16 kAnnouncementPort = 8080;
    private const string kDownloadServerIp = "127.0.0.1";
    private const UInt16 kDownloadServerPort = 8020;
    private const string kMulticastServerIp = "127.0.0.1";
    private const UInt16 kMulticastPbufPort = 8013;
    private const string kMulticastTestChannel = "test_channel";
    private const string kChatTestChannel = "chat_channel";
    private const string kChatUserName = "my_name";

    // member variables.
    private FunapiNetwork network_ = null;
    private FunapiHttpDownloader downloader_ = null;
    private FunapiAnnouncement announcement_ = new FunapiAnnouncement();
    private FunapiMulticastClient multicast_ = null;
    private FunapiChatClient chat_ = null;
    private bool with_protobuf_ = false;
    private bool with_session_reliability_ = false;
    private string announcement_url_ = "";
}
