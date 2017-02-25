using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Parameters;
using System.Diagnostics;
using Rhino.Collections;

namespace UserDataUtils
{
    public class CreateUserData : GH_Component, IGH_VariableParameterComponent
    {
        /// <summary>
        /// Initializes a new instance of the CreateUserData class.
        /// </summary>
        public CreateUserData()
          : base("Create Custom User Data", "CUD",
              "Creates a custom user dictionary which you can nest in another dictionary.",
              "Speckle", "User Data Utils")
        {
        }

        public override void AddedToDocument(GH_Document document)
        {
            base.AddedToDocument(document);
            foreach(var param in Params.Input)
            {
                param.ObjectChanged += (sender, e) =>
                {
                    Debug.WriteLine("(CUD:) param changed name.");
                    Rhino.RhinoApp.MainApplicationWindow.Invoke((Action)delegate { this.ExpireSolution(true); });
                };
            }
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("A", "A", "Data to attach to this key.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("User Data", "UD", "The user data as an Archivable Dictionary.", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var props = new ArchivableDictionary();

            for (int i = 0; i < Params.Input.Count; i++)
            {
                var key = Params.Input[i].NickName;

                object value = null;
                DA.GetData(i, ref value);

                if (value != null)
                {
                    GH_Number nmb = value as GH_Number;
                    if (nmb != null)
                        props.Set(key, nmb.Value);

                    if (value is double)
                        props.Set(key, (double)value);

                    if(value is int)
                        props.Set(key, (double)value);

                    GH_String str = value as GH_String;
                    if (str != null)
                        props.Set(key, str.Value);

                    if (value is string)
                        props.Set(key, (string)value);

                    GH_Boolean bol = value as GH_Boolean;
                    if (bol != null)
                        props.Set(key, bol.Value);

                    GH_ObjectWrapper temp = value as GH_ObjectWrapper;
                    if (temp != null)
                    {
                        ArchivableDictionary dict = ((GH_ObjectWrapper)value).Value as ArchivableDictionary;
                        if (dict != null)
                            props.Set(key, dict);
                    }

                    if (!props.ContainsKey(key))
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, key + " could not be set. Strings, numbers, booleans and ArchivableDictionary are the only supported types.");
                }
            }

            DA.SetData(0, props);
        }

        public bool CanInsertParameter(GH_ParameterSide side, int index)
        {
            if (side == GH_ParameterSide.Input) return true;
            return false;
        }

        public bool CanRemoveParameter(GH_ParameterSide side, int index)
        {
            if (side == GH_ParameterSide.Input) return true;
            return false;
        }

        public IGH_Param CreateParameter(GH_ParameterSide side, int index)
        {
            Grasshopper.Kernel.Parameters.Param_GenericObject param = new Param_GenericObject();

            param.Name = GH_ComponentParamServer.InventUniqueNickname("ABCDEFGHIJKLMNOPQRSTUVWXYZ", Params.Input);
            param.NickName = param.Name;
            param.Description = "Property Name";
            param.Optional = false;
            param.Access = GH_ParamAccess.item;

            param.ObjectChanged += (sender, e) =>
            {
                Debug.WriteLine("(CUD:) param changed name.");
                Rhino.RhinoApp.MainApplicationWindow.Invoke((Action)delegate { this.ExpireSolution(true); });
            };

            return param;
        }

        public bool DestroyParameter(GH_ParameterSide side, int index)
        {
            return side == GH_ParameterSide.Input;
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
            get { return new Guid("{d0350df1-fd31-4ae9-9154-815334c0b853}"); }
        }
    }


}