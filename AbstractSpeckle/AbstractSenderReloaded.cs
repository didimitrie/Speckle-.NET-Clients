using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Diagnostics;
using System.Threading;

using Grasshopper.Kernel.Parameters;
using GH_IO.Serialization;
using System.Dynamic;
using Newtonsoft.Json;
using SpeckleAbstract;

namespace SpeckleAbstract
{
    public class AbstractSenderReloaded : GH_Component, IGH_VariableParameterComponent
    {
        string streamId;
        string serialisedProxy;
        bool ready;

        Action expireComponentAction;

        SpeckleApiProxy proxy;

        public AbstractSenderReloaded()
          : base("AbstractSenderReloaded", "AbstractSenderReloaded",
              "AbstractSenderReloaded",
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
            ready = false;

            if (serialisedProxy == null)
            {
                Debug.WriteLine("Will create a new stream.");
                // TODO: Prompt for identity and use the values as arugments
                proxy = new SpeckleApiProxy(SpeckleRoles.Sender);
            }
            else
            {
                Debug.WriteLine("Will join an existing stream as sender.");
                proxy = new SpeckleApiProxy(serialisedProxy);
            }

            proxy.OnInitReady += (sender, e) =>
            {
                //System.Windows.Forms.MessageBox.Show("Got ready!");
                ready = true;
            };

            proxy.OnStreamIdReceived += (sender, e) =>
            {
                streamId = (string)e.Data;
            };

            proxy.OnDataSent += (sender, e) =>
            {
                Debug.WriteLine("Data was sent. Stop the loading bar :) Wait. What loading bar? ");
            };

            //for Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
            expireComponentAction = () =>
            {
                this.ExpireSolution(true);
            };

            this.ObjectChanged += (sender, e) =>
            {
                Debug.WriteLine("Object changed event!");
                updateMetadata();
            };

            foreach (var param in Params.Input)
            {
                param.ObjectChanged += (sender, e) =>
                {
                    updateMetadata();
                };
            }
        }

        public override void RemovedFromDocument(GH_Document document)
        {
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
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            if (!ready) return;
            updateData();
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
            param.AttributesChanged += (sender, e) =>
            {
                Debug.WriteLine("Attributes have changed! (of param)");
            };
            param.ObjectChanged += (sender, e) =>
            {
                updateMetadata();
            };
            return param;
        }

        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, int index)
        {
            //updateMetadata(); //solve instance is called on param destroy
            return true;
        }

        void IGH_VariableParameterComponent.VariableParameterMaintenance()
        {
            //updateMetadata(); // solve instance is called on param destroy or create it seems
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

        public dynamic getMetadata()
        {
            dynamic metadata = new ExpandoObject();
            metadata.streamName = NickName;
            metadata.controllers = new List<ExpandoObject>();
            metadata.structure = new List<ExpandoObject>();

            int startIndex = 0;
            foreach (IGH_Param myParam in Params.Input)
            {
                dynamic item = new ExpandoObject();
                item.name = myParam.NickName == null ? myParam.Name : myParam.NickName;
                item.guid = myParam.InstanceGuid.ToString();
                item.topology = getTopology(myParam);
                item.objectCount = myParam.VolatileDataCount;
                item.startIndex = startIndex;
                metadata.structure.Add(item);
                startIndex += myParam.VolatileDataCount;
            }
            return metadata;
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


            if (proxy != null)
                proxy.updateMetadata(getMetadata());
            else
                Debug.WriteLine("Ouuups.");
        }

        public void updateData()
        {

            if (proxy != null)
                proxy.updateData(getData(), getMetadata());
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
            get { return new Guid("{abe8523b-a8c8-4aed-b2bb-6a54066e5650}"); }
        }
    }
}
