using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;

using SpeckleAbstract;
using GH_IO.Serialization;
using System.Diagnostics;
using Grasshopper.Kernel.Parameters;

namespace SpeckleAbstract
{
    public class AbstractReceiver : GH_Component, IGH_VariableParameterComponent
    {
        string streamId;
        string serialisedProxy;
        List<string> senderGuids;

        SpeckleApiProxy proxy;

        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public AbstractReceiver()
          : base("AbstractReceiver", "AbstractReceiver",
              "Abstract data receiver",
              "SocketTest", "SocketTest")
        {
        }

        public override bool Write(GH_IWriter writer)
        {
            if (proxy != null)
                writer.SetString("serialisedProxy", proxy.ToString());
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetString("serialisedProxy", ref serialisedProxy);
            return base.Read(reader);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);

            senderGuids = new List<string>();

            if (serialisedProxy != null)
            {
                proxy = new SpeckleApiProxy(serialisedProxy);
                streamId = proxy.getStreamId();
                registerProxyEvents();
            }

        }

        public override void RemovedFromDocument(GH_Document document)
        {
            if (proxy != null)
                proxy.Dispose();
            base.RemovedFromDocument(document);
        }

        public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
        {
            if (context == GH_DocumentContext.Close)
            {
                proxy.Dispose();
            }
            base.DocumentContextChanged(document, context);
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("streamId", "streamId", "Which speckle stream do you want to connect to?", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string inputId = null;
            DA.GetData(0, ref inputId);

            if (inputId == null)
            {
                Message = "No streamId to listen to.";
                return;
            }
            if (inputId != streamId)
            {
                streamId = inputId;

                if (proxy != null) proxy.Dispose();

                proxy = new SpeckleApiProxy(SpeckleRoles.Receiver, _streamId: streamId);
                registerProxyEvents();
            }

        }

        void registerProxyEvents()
        {
            if (proxy == null) return;

            proxy.OnMetaUpdate += (sender, e) =>
            {
                Debug.WriteLine("RECEIVER: metadata update: " + e.EventInfo);
                var ee = e;

                this.Name = this.NickName = (string)e.Data.streamName;

                diffStructure(e.Data.parameters);
            };

            proxy.OnLiveUpdate += (sender, e) =>
            {
                Debug.WriteLine("Received live update event: " + e.EventInfo);
                this.Name = this.NickName = (string)e.Data.liveInstance.name;
                diffStructure(e.Data.liveInstance.structure);
            };

            proxy.OnHistoryUpdate += (sender, e) =>
            {
                Debug.WriteLine("Received history update event: " + e.EventInfo);
            };

            proxy.OnVolatileMessage += (sender, e) =>
            {
                Debug.WriteLine("RECEIVER: got volatile message: " + e.EventInfo);
                Debug.WriteLine((string)e.Data);
            };
        }

        public void diffStructure(dynamic data)
        {
            // data.parameters = array of { name, guid, topology }
            if (data == null) return;

            List<string> newGuids = new List<string>(), oldGuids = new List<string>();

            foreach (var param in data) newGuids.Add((string)param.guid);
            foreach (var param in Params.Output) oldGuids.Add(param.Name);

            var toRemove = oldGuids.Except(newGuids).ToList();
            var toAdd = newGuids.Except(oldGuids).ToList();

            Debug.WriteLine("Diffing Stuff: ");
            Debug.WriteLine("To remove:" + toRemove.Count);
            Debug.WriteLine("To add:" + toAdd.Count);

            foreach (var param in toRemove)
            {
                var myParam = Params.Output.FirstOrDefault(item => { return item.Name == param; });
                if (myParam != null)
                {
                    Params.UnregisterOutputParameter(myParam);
                    Debug.WriteLine("Deregistered a param: " + param);
                }
            }

            int count = 0;
            foreach (var param in data)
            {
                var myParam = Params.Output.FirstOrDefault(item => { return item.Name == (string)param.guid; });
                if (myParam != null)
                {
                    myParam.NickName = param.name;
                }
                if (toAdd.Contains((string)param.guid))
                {
                    Debug.WriteLine("RECEIVER: Adding param " + (string)param.guid + "///" + (string)param.name);
                    Param_GenericObject newParam = getGhParameter(param);
                    Params.RegisterOutputParam(newParam, count);
                }
                count++;
            }

            Params.OnParametersChanged();

        }

        private Param_GenericObject getGhParameter(dynamic param)
        {
            Param_GenericObject newParam = new Param_GenericObject();
            newParam.Name = (string)param.guid;
            newParam.NickName = (string)param.name;
            newParam.MutableNickName = false;
            newParam.Access = GH_ParamAccess.tree;
            return newParam;
        }

        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, Int32 index)
        {
            return false;
        }
        bool IGH_VariableParameterComponent.CanRemoveParameter(GH_ParameterSide side, Int32 index)
        {
            return false;
        }
        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, Int32 index)
        {
            return false;
        }
        IGH_Param IGH_VariableParameterComponent.CreateParameter(GH_ParameterSide side, Int32 index)
        {
            return null;
        }

        public void VariableParameterMaintenance()
        {
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{9d04ec58-af99-49cd-9629-1b12ca13d102}"); }
        }
    }
}