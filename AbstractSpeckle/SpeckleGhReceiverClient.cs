using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;

using SpeckleAbstract;
using GH_IO.Serialization;
using System.Diagnostics;
using Grasshopper.Kernel.Parameters;

using SpeckleClient;
using SpeckleGhRhConverter;
using Grasshopper;
using Grasshopper.Kernel.Data;

namespace SpeckleAbstract
{
    public class SpeckleGhReceiverClient : GH_Component, IGH_VariableParameterComponent
    {
        string streamId;
        string serialisedReceiver;
        List<string> senderGuids;

        SpeckleReceiver myReceiver;
        List<SpeckleLayer> layers;
        List<object> objects;

        Action expireComponentAction;

        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public SpeckleGhReceiverClient()
          : base("Speckle Receiver", "Speckle Receiver",
              "Speckle Receier",
              "Speckle", "Speckle")
        {
        }

        public override bool Write(GH_IWriter writer)
        {
            if (myReceiver != null)
                writer.SetString("serialisedReceiver", myReceiver.ToString());
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            reader.TryGetString("serialisedReceiver", ref serialisedReceiver);
            return base.Read(reader);
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);

            senderGuids = new List<string>();

            if (serialisedReceiver != null)
            {
                myReceiver = new SpeckleReceiver(serialisedReceiver, new GhRhConveter(true, true));
                streamId = myReceiver.getStreamId();
                registermyReceiverEvents();
            }
            else
            {
                // do nothing, init 
            }

            expireComponentAction = () => this.ExpireSolution(true);
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            if (myReceiver != null)
                myReceiver.Dispose();
            base.RemovedFromDocument(document);
        }

        public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
        {
            if (context == GH_DocumentContext.Close)
            {
                myReceiver.Dispose();
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
            Debug.WriteLine("StreamId: " + streamId + " Read ID: " + inputId);

            if (inputId == null && streamId == null)
            {
                Debug.WriteLine("No streamId to listen to.");
                return;
            }
            else if ((inputId != streamId) && (inputId!=null))
            {
                Debug.WriteLine("changing streamid");
                streamId = inputId;
                if (myReceiver != null) myReceiver.Dispose();
                myReceiver = new SpeckleReceiver(new SpeckleServer(@"http://10.211.55.2:8080", @"ws://10.211.55.2:8080", "asdf", streamId), new GhRhConveter(true, true));
                registermyReceiverEvents();
                Message = "";
                return;
            }

            setObjects(DA, objects, layers);
        }

        void registermyReceiverEvents()
        {
            if (myReceiver == null) return;

            myReceiver.OnReady += (sender, e) =>
            {
                this.Name = this.NickName = (string)e.Data.name;
                diffStructure(e.Data.layers);
                layers = e.Data.layers;
                objects = e.Data.objects;

                Debug.WriteLine("ready event");
                Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
            };

            myReceiver.OnMetadata += (sender, e) =>
            {
                this.Name = this.NickName = (string)e.Data.name;
                diffStructure(e.Data.layers);
                layers = e.Data.layers;

                Debug.WriteLine("metadata event");
                //Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
            };

            myReceiver.OnData += (sender, e) =>
            {
                Debug.WriteLine("RECEIVER: Received live update event: " + e.EventInfo);
                this.Name = this.NickName = (string)e.Data.name;
                diffStructure(e.Data.layers);
                layers = e.Data.layers;
                objects = e.Data.objects;

                Debug.WriteLine("data event");
                Rhino.RhinoApp.MainApplicationWindow.Invoke(expireComponentAction);
            };

            myReceiver.OnHistory += (sender, e) =>
            {
                Debug.WriteLine("Received history update event: " + e.EventInfo);
            };

            myReceiver.OnVolatileMessage += OnVolatileMessage;
        }

        public virtual void OnVolatileMessage(object source, SpeckleEventArgs e)
        {
            Debug.WriteLine("Got a volatile message.");
        }
        //public void OnVolatileMessage(object source, SpeckleEventArgs e)
        //{
        //    Debug.WriteLine("RECEIVER: got volatile message: " + e.EventInfo);
        //    Debug.WriteLine((string)e.Data);
        //}

        public void diffStructure(List<SpeckleLayer> newLayers)
        {
            dynamic diffResult = SpeckleLayer.diffLayers(getLayers(), newLayers);

            foreach (SpeckleLayer layer in diffResult.toRemove)
            {
                var myparam = Params.Output.FirstOrDefault(item => { return item.Name == layer.guid; });

                if (myparam != null)
                    Params.UnregisterOutputParameter(myparam);
            }

            foreach (var layer in diffResult.toAdd)
            {
                Param_GenericObject newParam = getGhParameter(layer);
                Params.RegisterOutputParam(newParam, layer.orderIndex);
            }

            foreach (var layer in diffResult.toUpdate)
            {
                var myparam = Params.Output.FirstOrDefault(item => { return item.Name == layer.guid; });
                myparam.NickName = layer.name;
            }

            Params.OnParametersChanged();

        }

        public void setObjects(IGH_DataAccess DA, List<object> objects, List<SpeckleLayer> structure)
        {
            if (structure == null) return;
            foreach (SpeckleLayer layer in structure)
            {
                var subset = objects.GetRange(layer.startIndex, layer.objectCount);

                if (layer.topology == "")
                    DA.SetDataList(layer.orderIndex, subset);
                else
                {
                    //HIC SVNT DRACONES
                    var tree = new DataTree<object>();
                    var treeTopo = layer.topology.Split(' ');
                    int subsetCount = 0;
                    foreach (var branch in treeTopo)
                    {
                        if (branch != "")
                        {
                            var branchTopo = branch.Split('-')[0].Split(';');
                            var branchIndexes = new List<int>();
                            foreach (var t in branchTopo) branchIndexes.Add(Convert.ToInt32(t));

                            var elCount = Convert.ToInt32(branch.Split('-')[1]);
                            GH_Path myPath = new GH_Path(branchIndexes.ToArray());

                            for (int i = 0; i < elCount; i++)
                                tree.EnsurePath(myPath).Add(subset[subsetCount + i]);
                            subsetCount += elCount;
                        }
                    }
                    DA.SetDataTree(layer.orderIndex, tree);
                }
            }
        }

        #region Variable Parm

        private Param_GenericObject getGhParameter(SpeckleLayer param)
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

        #endregion

        #region Layer Helpers

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
            foreach (IGH_Param myParam in Params.Output)
            {
                // NOTE: For gh receivers, we store the original guid of the sender component layer inside the parametr name.
                SpeckleLayer myLayer = new SpeckleLayer(
                    myParam.NickName,
                    myParam.Name /* aka the orignal guid*/, getTopology(myParam),
                    myParam.VolatileDataCount,
                    startIndex,
                    count);

                layers.Add(myLayer);
                startIndex += myParam.VolatileDataCount;
                count++;
            }
            return layers;
        }

        #endregion

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