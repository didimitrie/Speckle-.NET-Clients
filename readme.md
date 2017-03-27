# Speckle .NET

This is one big repo containing the following libraries:
- Speckle Client (folder `AbstractSpeckle`).
- Speckle Popup
- Speckle Grasshopper & Speckle GhRh Converter
- Speckle Grasshopper User Data Utils 

## Speckle Client
This is the base library that provides two classes that you should really care about: 

### Speckle Receiver
Exposes a series of events that get triggered when the sender emits data. 
```csharp
myReceiver = new SpeckleReceiver(API_URL, API_TOKEN, STREAM_TO_LISTEN_TO, GEOMETRY_CONVERTER);
```

### Speckle Sender
Exposes a series of methods that allow you to send data (metadata + geometry) as well as just metadata (layer names, etc). 

```csharp
mySender = new SpeckleSender(API_URL, API_TOKEN, GEOMETRY_CONVERTER);
```

Also exposed is a virtual class `Speckle Converter`. You need to implement this class if you want to be able to send data back and forth. It translates geometry from application_x format to a speckle intermediary format.  

Look for an example implementation in the Speckle Grasshopper components. 

## Speckle Popup
A nifty little dialog that asks you which Speckle Account to use or allows you to register a new Speckle Account with a server of your choosing. It keeps track of all the accounts you use in `C:\Users\[YOUR USERNAME]\AppData\Local\SpeckleSettings` as txt files. 

It is used internally by the grasshopper components as a dialog whenever a new receiver or sender component is created: 

```csharp
var myForm = new SpecklePopup.MainWindow();
myForm.ShowDialog();
```

## Speckle Grasshopper & Converter
Exposes three components: 

### Sender

Sends data. Uses ZUI. 

### Receiver

Receives data. 

### Extended Receiver
Shows how you can derive from the comopnents and implement your own behaviour for events, etc. 

## Data Utils

#### Set User Data (SUD)
Attaches a Dictionary created with the component below to objects that support it. It's slightly aggressive, as it makes the following conversions in order to set the dictionaries: 
- Polyline, Circle, Rectangle => NurbsCurve
- Box => Brep

Takes as inputs an object and a variable list of other keys. 

#### Create User Data (CUD)
ZUI based. Creates a custom ArchivableDictionary based on the given inputs (either `double`, `string` or a another `ArchivableDictionary`). Can be used to create nested properties.

#### Get User Data (GUD)
Spits out an object's user dictionary (if any).

#### Expand User Data (EUD)
Expands a dictionary into its component keys (non-recursive). Recurse it yourself, yo!

#### Export to CSV (CSVUD)
Does what it says on the label, but recursively. Nested properties are handled: `rootProp.childProp1.childProp2`. If a dictionary does not have the respective key, `null` is placed.
Right click and `Save to file` to save the output to a text file.

#### Export to JSON (JUD)
Spits out a json string of the dictionary array provided. Easy peasy. Use pretty formating if needing a human readable file. 
Right click and `Save to file` to save the output to a text file.
