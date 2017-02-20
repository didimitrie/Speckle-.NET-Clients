## Speckle Data Utils

Create, Get and Set UserDictionary properties for various CommonObject types.

### Why? 
Speckle Server handles all these props. Woot!

### Component list and functionality: 

#### Set User Data (SUD)
ZUI based, sets keys (either `double`, `string` or a another `ArchivableDictionary`) in a user dictionary if the object provided supports it. It's slightly aggressive, as it makes the following conversions in order to set the dictionaries: 
- Polyline, Circle, Rectangle => NurbsCurve
- Box => Brep

Takes as inputs an object and a variable list of other keys. 

#### Create User Data (CUD)
Creates a custom ArchivableDictionary based on the given inputs (either `double`, `string` or a another `ArchivableDictionary`). Can be used to create nested properties.

#### Get User Data (GUD)
Spits out an object's user dictionary (if any).

#### Expand User Data (EUD)
Expands a dictionary into its component keys (non-recursive). 

#### Export to CSV (CSVUD)
Does what it says on the label, but recursively. Nested properties are handled: `rootProp.childProp1.childProp2`. If a dictionary does not have the respective key, `null` is placed.

#### Export to JSON (JUD)
Spits out a json string of the dictionary array provided. Easy peasy. 

#### Filter by User Data (FUD) [DEPRECATED/NOT IN USE]
Given an object and a property key, it will search for that key in the user dictionary of the object. If it finds it, it outputs both the object as well as the property value. Subsequent filtering can be done via standard gh components. Previous keys are retained & filter operations can be chained.
