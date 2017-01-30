### Speckle Data Utils

Set, Get, and Filter based on UserDictionary properties for various CommonObject types. 

#### Set User Data (SUD)
ZUI based, sets keys (either `double` or `string`) in a user dictionary if the object provided supports it. It's slightly aggressive, as it makes the following conversions: 
- Polyline, Circle, Rectangle => NurbsCurve
- Box => Brep

#### Get User Data (GUD)
Spits out all the keys in the object's user dictionary separately from their respective values.

#### Filter by User Data (FUD)
Given an object and a property key, it will search for that key in the user dictionary of the object. If it finds it, it outputs both the object as well as the property value.
