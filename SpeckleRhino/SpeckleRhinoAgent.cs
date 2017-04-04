using SpeckleClient;
using SpeckleGhRhConverter;

namespace SpeckleRhino
{
    public class SpeckleRhinoAgent
    {

        public string serializedSender { get; set; }
        public string serializedReceiver { get; set; }

        public SpeckleSender sender;
        public SpeckleReceiver receiver;

        public SpeckleRhinoAgent()
        {
            SpeckleRhinoPlugIn.Instance.SpeckleAgent = this;
        }

        public void Init()
        {
            if (serializedSender != null &&
               serializedReceiver != null)
            {
                //set 
                //TODO

                sender = new SpeckleSender(serializedSender, new GhRhConveter(true, true));
                receiver = new SpeckleReceiver(serializedReceiver, new GhRhConveter(true, true));


            } else {
                //do popup stuff
            }
        }
    }
}
