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
    public class SetUserDataComponent : GH_Component
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
            pManager.AddGenericParameter("Object", "O", "Object to attach user data to.", GH_ParamAccess.item);
            pManager.AddGenericParameter("User Data", "D", "Data to attach.", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Object", "O", "Object with user data.", GH_ParamAccess.item);
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

            object value = null;
            DA.GetData(1, ref value);

            GH_ObjectWrapper temp = value as GH_ObjectWrapper;
            if(temp==null)
            {
                DA.SetData(0, obj);
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Could not cast object to GH_ObjectWrapper.");
                return;
            }

            ArchivableDictionary dict = ((GH_ObjectWrapper)value).Value as ArchivableDictionary;
            if (dict == null)
            {
                DA.SetData(0, obj);
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Could not cast object to Dictionary.");
                return;
            }

            myObj.UserDictionary.ReplaceContentsWith(dict);

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
