using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper.Kernel.Parameters;
using System.Diagnostics;

using System.Linq;
using Grasshopper.Kernel.Types;
using Rhino.Runtime;
using Rhino.Collections;

namespace UserDataUtils
{
    public class SetUserDataComponent : GH_Component, IGH_VariableParameterComponent
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public SetUserDataComponent()
          : base("Set User Data", "SUD",
              "Sets user data to an object.",
              "Speckle", "User Data Utils")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Object", "O", "Object to set user data to.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Object", "O", "Object with user data.", GH_ParamAccess.item);
        }

        bool IGH_VariableParameterComponent.CanInsertParameter(GH_ParameterSide side, int index)
        {
            if (side == GH_ParameterSide.Input && index > 0)
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
            if (side == GH_ParameterSide.Input && Params.Input.Count > 1 && index > 0)
                return true;
            else
                return false;
        }

        IGH_Param IGH_VariableParameterComponent.CreateParameter(GH_ParameterSide side, int index)
        {
            Grasshopper.Kernel.Parameters.Param_GenericObject param = new Param_GenericObject();

            param.Name = GH_ComponentParamServer.InventUniqueNickname("ABCDEFGHIJKLMNOPQRSTUVWXYZ", Params.Input);
            param.NickName = param.Name;
            param.Description = "Property Name";
            param.Optional = false;
            param.Access = GH_ParamAccess.item;

            param.ObjectChanged += (sender, e) =>
            {
                Debug.WriteLine("param changed name.");
                Rhino.RhinoApp.MainApplicationWindow.Invoke((Action)delegate { this.ExpireSolution(true); });
            };

            return param;
        }

        bool IGH_VariableParameterComponent.DestroyParameter(GH_ParameterSide side, int index)
        {
            if (side == GH_ParameterSide.Input && index > 0)
                return true;
            return false;
        }

        void IGH_VariableParameterComponent.VariableParameterMaintenance()
        {
        }


        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object obj = null;
            DA.GetData(0, ref obj);

            // Remarks:
            // 1) why lines, rectangles or *any* rhinocommon object can't have user dicts?
            // 2) @David: i hate grasshopper typecasting and the hassle below
            // (why isn't there a GH_DefaultType, where i can access the .Value regardless of type...?)

            GeometryBase myObj = null;

            GH_Mesh mesh = obj as GH_Mesh;
            if (mesh != null)
                myObj = mesh.Value;

            GH_Brep brep = obj as GH_Brep;
            if (brep != null)
                myObj = brep.Value;

            GH_Surface srf = obj as GH_Surface;
            if (srf != null)
                myObj = srf.Value;

            GH_Box box = obj as GH_Box;
            if (box != null)
                myObj = box.Value.ToBrep();

            GH_Curve crv = obj as GH_Curve;
            if (crv != null)
                myObj = crv.Value;

            GH_Line line = obj as GH_Line;
            if (line != null)
                myObj = line.Value.ToNurbsCurve();

            GH_Rectangle rect = obj as GH_Rectangle;
            if (rect != null)
                myObj = rect.Value.ToNurbsCurve();

            GH_Circle circle = obj as GH_Circle;
            if (circle != null)
                myObj = circle.Value.ToNurbsCurve();

            GH_Arc arc = obj as GH_Arc;
            if (arc != null)
                myObj = arc.Value.ToNurbsCurve();

            GH_Point pt = obj as GH_Point;
            if (pt != null)
                myObj = new Point(pt.Value);

            if (myObj == null)
            {
                // get the object out
                DA.SetData(0, obj);
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Failed to set user dictionary to object. Probably an unsupported type.");
                return;
            }

            myObj.UserDictionary.Clear();

            for (int i = 1; i < Params.Input.Count; i++)
            {
                var key = Params.Input[i].NickName != "" ? Params.Input[i].NickName : "unnamed";
                object value = null;
                DA.GetData(i, ref value);

                if (value != null)
                {
                    GH_Number nmb = value as GH_Number;
                    if (nmb!=null)
                        myObj.UserDictionary.Set(key, nmb.Value);

                    if (value is double)
                        myObj.UserDictionary.Set(key, (double)value);

                    if (value is int)
                        myObj.UserDictionary.Set(key, (double)value);

                    GH_String str = value as GH_String;
                    if(str!=null)
                        myObj.UserDictionary.Set(key, str.Value);

                    if (value is string)
                        myObj.UserDictionary.Set(key, (string)value);

                    GH_Boolean bol = value as GH_Boolean;
                    if (bol != null)
                        myObj.UserDictionary.Set(key, bol.Value);

                    GH_ObjectWrapper temp = value as GH_ObjectWrapper;
                    if (temp != null)
                    {
                        ArchivableDictionary dict = ((GH_ObjectWrapper)value).Value as ArchivableDictionary;
                        if (dict != null)
                            myObj.UserDictionary.Set(key, dict);
                    }

                    if (!myObj.UserDictionary.ContainsKey(key))
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, key + " could not be set. Strings, numbers and ArchivableDictionary are the supported types.");
                }

            }

            DA.SetData(0, myObj);
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

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{4edbb242-15e2-4b30-89c4-fa800d156250}"); }
        }
    }
}
