﻿using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Diagnostics;
using System.Threading;

using Grasshopper.Kernel.Parameters;
using GH_IO.Serialization;

// using the two Speckle Libs
using SpeckleClient;
using SpeckleGhRhConverter;

using System.Dynamic;
using System.ComponentModel;
using System.Linq;

namespace SpeckleAbstract
{
    public class SpeckleGhSenderClient : GH_Component, IGH_VariableParameterComponent
    {
        string streamId, wsSessionId;
        string serialisedSender;
        bool ready;

        Action expireComponentAction;

        SpeckleSender mySender;

        public SpeckleGhSenderClient()
          : base("Speckle Sender", "Speckle Sender",
              "Speckle Sender",
              "Speckle", "Speckle")
        {

        }

        public override bool Write(GH_IWriter writer)
        {
            if (mySender != null)
                writer.SetString("specklesender", mySender.ToString());

            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetString("specklesender", ref serialisedSender);

            return base.Read(reader);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);

            ready = false;

            if (serialisedSender != null)
            {
                // means we're an old sender, deserialising right now
                mySender = new SpeckleClient.SpeckleSender(serialisedSender, new GhRhConveter(true, true));
            }
            else
            {
                // means we're new! TODO: ask for server details.
                // below assuming some stuff.

                //remote testing:
                //mySender = new SpeckleSender(new SpeckleServer(@"https://5th.one", @"wss://5th.one", "asdf"), new GhConveter()); 

                // local testing:
                mySender = new SpeckleSender(new SpeckleServer(@"http://10.211.55.2:8080", @"ws://10.211.55.2:8080", "asdf"), new GhRhConveter(true, true));
            }

            mySender.OnReady += (sender, e) =>
            {
                Debug.WriteLine("Sender ready:::" + (string)e.Data.streamId + ":::" + (string)e.Data.wsSessionId);
                this.ready = true;
                this.streamId = e.Data.streamId;
                this.wsSessionId = e.Data.wsSessionId;
            };

            mySender.OnDataSent += (sender, e) =>
            {
                Debug.WriteLine("Data was sent. Stop the loading bar :) Wait. What loading bar? The one luis wanted! Where is it? I dunno");
            };

            expireComponentAction = () => this.ExpireSolution(true);

            // events on objects
            this.ObjectChanged += (sender, e) => updateMetadata();

            // events on parameters
            foreach (var param in Params.Input)
                param.ObjectChanged += (sender, e) => updateMetadata();
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            mySender.Dispose();
            base.RemovedFromDocument(document);
        }

        public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
        {
            if (context == GH_DocumentContext.Close)
                mySender.Dispose();

            base.DocumentContextChanged(document, context);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("URL", "URL", "Link to the latest uploaded file.", GH_ParamAccess.item);
            pManager.AddTextParameter("ID", "ID", "Link to the latest uploaded file.", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!ready) return;

            updateData();

            DA.SetData(0, mySender.getServer() + @"/stream?streamId=" + mySender.getStreamId());
            DA.SetData(1, mySender.getStreamId());
        }


        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, int index)
        {
            if (side == GH_ParameterSide.Input)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        bool IGH_VariableParameterComponent.CanRemoveParameter(GH_ParameterSide side, int index)
        {
            //We can only remove from the input
            if (side == GH_ParameterSide.Input && Params.Input.Count > 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        IGH_Param IGH_VariableParameterComponent.CreateParameter(GH_ParameterSide side, int index)
        {
            Grasshopper.Kernel.Parameters.Param_GenericObject param = new Param_GenericObject();

            param.Name = GH_ComponentParamServer.InventUniqueNickname("ABCDEFGHIJKLMNOPQRSTUVWXYZ", Params.Input);
            param.NickName = param.Name;
            param.Description = "Things to be sent around.";
            param.Optional = true;
            param.Access = GH_ParamAccess.tree;

            param.AttributesChanged += (sender, e) => Debug.WriteLine("Attributes have changed! (of param)");
            param.ObjectChanged += (sender, e) => updateMetadata();

            return param;
        }

        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, int index)
        {
            return true;
        }

        void IGH_VariableParameterComponent.VariableParameterMaintenance()
        {
        }


        public string getTopology(IGH_Param param)
        {
            string topology = "";
            foreach (Grasshopper.Kernel.Data.GH_Path mypath in param.VolatileData.Paths)
            {
                topology += mypath.ToString(false) + "-" + param.VolatileData.get_Branch(mypath).Count + " ";
            }
            return topology;
        }

        public List<SpeckleLayer> getLayers()
        {
            List<SpeckleLayer> layers = new List<SpeckleLayer>();
            int startIndex = 0;
            int count = 0;
            foreach (IGH_Param myParam in Params.Input)
            {
                SpeckleLayer myLayer = new SpeckleLayer(
                    myParam.NickName,
                    myParam.InstanceGuid.ToString(), 
                    getTopology(myParam),
                    myParam.VolatileDataCount,
                    startIndex,
                    count);

                layers.Add(myLayer);
                startIndex += myParam.VolatileDataCount;
                count++;
            }
            return layers;
        }

        public List<object> getData()
        {
            List<object> data = new List<dynamic>();
            foreach (IGH_Param myParam in Params.Input)
            {
                foreach (object o in myParam.VolatileData.AllData(false))
                    data.Add(o);
            }
            return data;
        }

        public void updateMetadata()
        {
            Debug.WriteLine("Component: UPDATING METADATA");
            mySender.sendMetadataUpdate(getLayers(), this.NickName);
        }

        public void updateData()
        {
            Debug.WriteLine("Component: UPDATING DATA");
            mySender.sendDataUpdate(getData(), getLayers(), this.NickName);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("{82564680-f008-4f29-bcfc-af782a9237ca}"); }
        }
    }
}