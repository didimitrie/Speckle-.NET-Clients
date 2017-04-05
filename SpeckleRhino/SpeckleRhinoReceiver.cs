using System;
using System.Diagnostics;
using SpeckleClient;
using SpeckleGhRhConverter;
using System.Collections.Generic;

namespace SpeckleRhino
{
    public class SpeckleRhinoReceiver : IDisposable
    {

        public string serializedReceiver { get; set; }
        public string apiUrl { get; set; }
        public string token { get; set; }
        public string streamId { get; set; }

        public SpeckleReceiver Receiver;

        List<SpeckleLayer> layers;
        List<object> objects;

        public SpeckleRhinoReceiver(string _inputId)
        {
            Init();
            Solve(_inputId);
        }

        #region Methods

        public void Init()
        {
            if (serializedReceiver != null)
            {
                //set 
                //TODO

                Receiver = new SpeckleReceiver(serializedReceiver, new GhRhConveter(true, true));

                streamId = Receiver.getStreamId();
                apiUrl = Receiver.getServer();
                token = Receiver.getToken();

                RegistermyReceiverEvents();

            } else {
                //do popup stuff
                var popup = new SpecklePopup.MainWindow();

                var some = new System.Windows.Interop.WindowInteropHelper(popup);
                some.Owner = Rhino.RhinoApp.MainWindowHandle();

                popup.ShowDialog();

                if (popup.restApi != null && popup.apitoken != null)
                {
                    apiUrl = popup.restApi;
                    token = popup.apitoken;
                }

            }
        }

        public void Solve(string _inputId)
        {
            string inputId = _inputId;

            Debug.WriteLine("StreamId: " + streamId + " Read ID: " + inputId);

            if (apiUrl == null || token == null)
            {
                Debug.WriteLine("No server.");
                return;
            }

            if (inputId == null && streamId == null)
            {
                Debug.WriteLine("No streamId to listen to.");
                return;
            }
            else if ((inputId != streamId) && (inputId != null))
            {
                Debug.WriteLine("changing streamid");
                streamId = inputId;
                if (Receiver != null) Receiver.Dispose();

                Receiver = new SpeckleReceiver(apiUrl, token, streamId, new GhRhConveter(true, true));

                RegistermyReceiverEvents();
                return;
            }

        }

        void RegistermyReceiverEvents()
        {
            if (Receiver == null) return;

            Receiver.OnDataMessage += OnDataMessage;

            Receiver.OnError += OnError;

            Receiver.OnReady += OnReady;

            Receiver.OnMetadata += OnMetadata;

            Receiver.OnData += OnData;

            Receiver.OnHistory += OnHistory;

            Receiver.OnMessage += OnVolatileMessage;

            Receiver.OnBroadcast += OnBroadcast;
        }

        #endregion

        #region Event Handlers

        public virtual void OnError(object source, SpeckleEventArgs e)
        {
            Debug.WriteLine("Hit OnError");
        }

        public virtual void OnDataMessage(object source, SpeckleEventArgs e)
        {
            Debug.WriteLine("Hit OnDataMessage");
        }

        public virtual void OnBroadcast(object source, SpeckleEventArgs e)
        {
            Debug.WriteLine("Hit Broadcast");
        }

        public virtual void OnVolatileMessage(object source, SpeckleEventArgs e)
        {
            Debug.WriteLine("Hit OnVolatileMessage");
        }

        public virtual void OnHistory(object source, SpeckleEventArgs e)
        {
            Debug.WriteLine("Received history update event: " + e.EventInfo);
        }

        public virtual void OnData(object source, SpeckleEventArgs e)
        {
            Debug.WriteLine("RECEIVER: Received live update event: " + e.EventInfo);
        }

        public virtual void OnMetadata(object source, SpeckleEventArgs e)
        {
            Debug.WriteLine("Hit OnMetadata");
        }

        public virtual void OnReady(object source, SpeckleEventArgs e)
        {
            Debug.WriteLine("Hit OnReady");

            //diffStructure(e.Data.layers);
            //layers = e.Data.layers;
            //objects = e.Data.objects;
        }

        

        public void Dispose()
        {
            if (Receiver != null)
                Receiver.Dispose();
        }

        #endregion
    }
}
