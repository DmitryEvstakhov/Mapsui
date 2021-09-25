# Mapsui iOS Getting Started

### Step 1 

Create new 'Single View App' in Visual Studio

### Step 2

```console
PM> Install-Package Mapsui.iOS -pre
```

### Step 3

Open ViewController.cs 

add namespaces:

```csharp
using Mapsui;
using Mapsui.UI.iOS;
using Mapsui.Utilities;
```

add code to ViewDidLoad() method:

```csharp
public override void ViewDidLoad()
{
   base.ViewDidLoad();

   var mapControl = new MapControl(View.Bounds);
   var map = new Map();
   map.Layers.Add(OpenStreetMap.CreateTileLayer("your-user-agent"));
   mapControl.Map = map;
   View = mapControl;
}
```

### Step 4

Run and you should see a map.