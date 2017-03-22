using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeckleClient
{
    /// <summary>
    /// Defines a speckle layer.
    /// </summary>
    [Serializable]
    public class SpeckleLayer
    {
        public string name, guid, topology;
        /// <summary>
        /// How many objects does this layer hold.
        /// </summary>
        public int objectCount;

        /// <summary>
        /// The list index of the first object this layer contains. 
        /// </summary>
        public int startIndex;

        /// <summary>
        /// Keeps track of the position of this layer in the layer stack.
        /// </summary>
        public int orderIndex;

        /// <summary>
        /// Extra stuff one can set around.
        /// </summary>
        public dynamic properties;

        /// <summary>
        /// Creates a new Speckle Layer.
        /// <para>Note: A Speckle Stream has one (flat) list of objects and one (flat) list of layers. In order to understand which object belongs to which layer, every layer must contain (a) the index of the first object and (b) the number of objects it contains. </para>
        /// <para>For example: let objects = [A, B, C, D, E, F]. let myLayer = { startIndex: 2, objectCount: 3} => objects C, D, E belong to this layer. </para>
        /// </summary>
        /// <param name="_name">Layer name</param>
        /// <param name="_guid">Layer guid. Must be unique, otherwise dragons.</param>
        /// <param name="_topology">If coming from grasshopper, get the topology description here.</param>
        /// <param name="_objectCount">Number of objects this layer contains.</param>
        /// <param name="_startIndex">The index of the first object this layer contains from the global object list.</param>
        /// <param name="_properties">Extra properties, if you want to. Would be cool to have stuff like colours and materials in here.</param>
        public SpeckleLayer(string _name, string _guid, string _topology, int _objectCount, int _startIndex, int _orderIndex, dynamic _properties = null)
        {
            name = _name; guid = _guid; topology = _topology; objectCount = _objectCount; startIndex = _startIndex; orderIndex = _orderIndex;
            properties = _properties;
        }

        /// <summary>
        /// Diffs between two lists of layers.
        /// </summary>
        /// <param name="oldLayers"></param>
        /// <param name="newLayers"></param>
        /// <returns>A dynamic object containing the following lists: toRemove, toAdd and toUpdate. </returns>
        public static dynamic diffLayers(List<SpeckleLayer> oldLayers, List<SpeckleLayer> newLayers)
        {
            dynamic returnValue = new ExpandoObject();
            returnValue.toRemove = oldLayers.Except(newLayers, new SpeckleLayerComparer()).ToList();
            returnValue.toAdd = newLayers.Except(oldLayers, new SpeckleLayerComparer()).ToList();
            returnValue.toUpdate = newLayers.Intersect(oldLayers, new SpeckleLayerComparer()).ToList();

            return returnValue;
        }

        /// <summary>
        /// Converts a list of expando objects to speckle layers [tries to].
        /// </summary>
        /// <param name="o">List to convert.</param>
        /// <returns></returns>
        public static List<SpeckleLayer> fromExpandoList(IEnumerable<dynamic> o)
        {
            List<SpeckleLayer> list = new List<SpeckleLayer>();
            foreach (var oo in o) list.Add(SpeckleLayer.fromExpando(oo));
            return list;
        }

        /// <summary>
        /// Converts one expando object to a speckle layer [tries to].
        /// </summary>
        /// <param name="o">ExpandoObject to covnert.</param>
        /// <returns></returns>
        public static SpeckleLayer fromExpando(dynamic o)
        {
            return new SpeckleLayer((string)o.name, (string)o.guid, (string)o.topology, (int)o.objectCount, (int)o.startIndex, (int)o.orderIndex, (dynamic)o.properties);
        }

    }

    /// <summary>
    /// Used for diffing between layers w/h linq.
    /// </summary>
    internal class SpeckleLayerComparer : IEqualityComparer<SpeckleLayer>
    {
        public bool Equals(SpeckleLayer x, SpeckleLayer y)
        {
            return x.guid == y.guid;
        }

        public int GetHashCode(SpeckleLayer obj)
        {
            return obj.guid.GetHashCode();
        }
    }

    /// <summary>
    /// (string) EventInfo (random metadata); (dynamic) Data (the actual event data, if any).
    /// </summary>
    public class SpeckleEventArgs : EventArgs
    {
        public string EventInfo;
        public dynamic Data;
        /// <summary>
        /// Creates a new speckle event args.
        /// </summary>
        /// <param name="text">Event Info</param>
        /// <param name="_Data">ExpandoObject with event data.</param>
        public SpeckleEventArgs(string text, dynamic _Data = null)
        {
            EventInfo = text; Data = _Data;
        }
    }

    public delegate void SpeckleEvents(object source, SpeckleEventArgs e);

    /// <summary>
    /// Standardises object conversion. The SpeckleApiProxy requires a converter object which should derive from this interface.
    /// </summary>
    [Serializable]
    public abstract class SpeckleConverter
    {
        /// <summary>
        /// These types will not be hashed and will be saved directly in the HistoryInstance.
        /// </summary>
        public string[] nonHashedTypes = { "404", "Number", "Boolean", "String", "Point", "Vector", "Line", "Interval", "Interval2d" };

        /// <summary>
        /// These types will be base64 encoded (if converter is set up to).
        /// </summary>
        public string[] encodedTypes = { "Curve", "Brep" };

        public bool encodeObjectsToSpeckle;
        public bool encodeObjectsToNative;

        public SpeckleConverter(bool _encodeObjectsToSpeckle, bool _encodeObjectsToNative)
        {
            encodeObjectsToSpeckle = _encodeObjectsToSpeckle;
            encodeObjectsToNative = _encodeObjectsToNative;
        }

        abstract public void commitCache();


        #region global convert functions

        /// <summary>
        /// Converts a list of objects. 
        /// </summary>
        /// <param name="objects">Objects to convert.</param>
        /// <param name="getEncoded">If true, should return a base64 encoded version of the object as well.</param>
        /// <param name="getAbstract">If set to false will not return speckle-parsable values for objects.</param>
        /// <returns>A list of dynamic objects.</returns>
        abstract public List<SpeckleObject> convert(IEnumerable<object> objects);

        /// <summary>
        /// Async conversion of a list of objects. Returns the result in a callback.
        /// </summary>
        /// <param name="objects">Objects to convert.</param>
        /// <param name="callback">Action to perform with the converted objects.</param>
        abstract public void convertAsync(IEnumerable<object> objects, Action<List<SpeckleObject>> callback);


        abstract public List<SpeckleObjectProperties> getObjectProperties(IEnumerable<object> objects);

        #endregion

        /// <summary>
        /// Encodes an object back to its native type.
        /// </summary>
        /// <param name="myObj">object to encode to native.</param>
        /// <returns></returns>
        abstract public object encodeObject(dynamic myObj, dynamic objectProperties = null);

        #region standard types

        public static SpeckleObject fromBoolean(bool b)
        {
            dynamic obj = new SpeckleObject();
            obj.type = "Boolean";
            obj.hash = "Boolean.NoHash";
            obj.value = b ? 1 : 0;
            return obj;
        }

        public static bool toBoolean(dynamic b)
        {
            return (int)b.value == 1;
        }

        public static SpeckleObject fromNumber(double num)
        {
            dynamic obj = new SpeckleObject();
            obj.type = "Number";
            obj.hash = "Number.NoHash";
            obj.value = num;
            return obj;
        }

        public static double toNumber(dynamic num)
        {
            return (double)num.value;
        }

        public static SpeckleObject fromString(string str)
        {
            dynamic obj = new SpeckleObject();
            obj.type = "String";
            obj.hash = "String.NoHash";
            obj.value = str;
            return obj;
        }

        public static string toString(dynamic str)
        {
            return (string)str.value;
        }

        #endregion

        #region utils

        /// <summary>
        /// Encodes a serialisable object in base64 string.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string getBase64(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Serialize(ms, obj);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        /// <summary>
        /// Encodes a serialisable object in a byte array.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static byte[] getBytes(object obj)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Serialize(ms, obj);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Hashes a given string using md5. Used to get object hashes. 
        /// </summary>
        /// <param name="str">What to hash.</param>
        /// <returns>a lowercase string of the md5 hash.</returns>
        public static string getHash(string str)
        {
            byte[] hash;
            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(str));
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hash)
                    sb.Append(b.ToString("X2"));

                return sb.ToString().ToLower();
            }
        }

        public static object getObjFromString(string base64String)
        {
            if (base64String == null) return null;
            byte[] bytes = Convert.FromBase64String(base64String);
            using (MemoryStream ms = new MemoryStream(bytes, 0, bytes.Length))
            {
                ms.Write(bytes, 0, bytes.Length);
                ms.Position = 0;
                return new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter().Deserialize(ms);
            }
        }

        #endregion

        /// <summary>
        /// Spits out a string descriptor, ie "grasshopper-converter", or "rhino-converter". Used for reiniatisation of the API Proxy.
        /// </summary>
        /// <returns></returns>
        abstract public dynamic description();
    }

    /// <summary>
    /// Class that flexibily defines what a SpeckleObject is.
    /// </summary>
    [Serializable]
    public class SpeckleObject
    {
        public string type;
        public string hash;
        public dynamic value; // expandoobject
        public dynamic properties; // expandoobject
        public string encodedValue;
    }

    [Serializable]
    public class SpeckleObjectProperties
    {
        public int objectIndex;
        public object properties;

        public SpeckleObjectProperties(int _objectIndex, object _properties)
        {
            objectIndex = _objectIndex;
            properties = _properties;
        }
    }

    /// <summary>
    /// In progress. Not used yet.
    /// </summary>
    [Serializable]
    public class SpeckleClientDocument
    {
        public string documentGuid { get; set; }
        public string documentName { get; set; }
        /// <summary>
        /// RH, GH, DYNAMO, etc.
        /// </summary>
        public string documentType { get; set; }
    }
}
