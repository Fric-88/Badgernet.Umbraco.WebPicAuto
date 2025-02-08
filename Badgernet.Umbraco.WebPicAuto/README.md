
# Badgernet.Umbraco.WebPicAuto

### This package will resize your images when you upload them in Umbraco. It can also convert them to .webp format to save additional storage space.

Currently only local files are supported. Umbraco Cloud / Azure Blob Storage support may be added in later releases.


\
[![NuGet](https://img.shields.io/nuget/dt/Badgernet.Umbraco.WebPicAuto?ImageResizer?label=Downloads&color=green)](https://www.nuget.org/packages/Badgernet.Umbraco.WebPicAuto/)

---

# Installation
Simply add the package by using dotnet add package to install the latest version:
```
dotnet add package Badgernet.Umbraco.WebPicAuto
```

---

# Uninstallation
```
dotnet remove package Badgernet.Umbraco.WebPicAuto
```
After uninstalling the package, make sure to delete the folder "App_Plugins/Badgernet.Umbraco.WebPicAuto", otherwise the dashboard may still be displayed in the backoffice. 

---

# Important if using uSync
This package will also process any images imported using the uSync import feature. Be sure to turn off the converter / resizer (via the dashboard) if you do not want them to be processed.

---

# Changelog
#### Version 1.1.4
- Changes how images are processed in the background

#### Version 1.1.3
- Adds support for processing existing image files

#### Version 1.1.2
- Fix: Plays nice with uSync

#### Version 1.1.1
- Fix: Adjusted namespace to "Badgernet.WebPicAuto" to prevent conflicts.
- Fix: Removed "WpaSettings.json" from the package.
- Converter now enabled by default

#### Version 1.1.0
- Backoffice dashboard

#### Version 1.0.1
- Added option to downscale images without keeping aspect ratio
- Provide target dimensions in filename -> "wparesize_800_600_ImageName.png"

---

# Settings
You can change these default settings by accessing the "Converter/Resizer" dashboard in the Media section in backoffice.
Settings are stored in a file: "App_Plugins/Badgernet.Umbraco.WebPicAuto/Backoffice/WpaSettings.json"


## Resizer enabled
Turns image resizing on or off

## Max width
Resizer will scale images down to fit max width value

## Max height
Resizer will scale images down to fit max height

## Ignore aspect ratio
By default, resizing will maintain image aspect ratio.

## Converter enabled
Turns image converting on or off

## Convert mode
#### Image encoding type
"Lossy" mode will produce smaller file size images. <- this is the preferred / default mode  \
"Lossless" mode will produce better quality images.

### Convert quality
#### Value from 1 to 100
Quality of conversion, lower value will produce smaller file size images but image quality will also be worse.

### Keep original images
If turned on, original images will not be deleted (wwwroot/media/***)

### Ignore keyword
Any images containing this keyword in its filename will be ignored by this package. -> "wpaignore_IMG01012024.png" would not get processed.

---

# Credits
Thanks to everybody at [@Our Umbraco Forum]([https://our.umbraco.com/forum/]) for their helpful tips. \
This project was inspired by [@VirjdagOnline.ImageResizer]([https://www.nuget.org/packages/VrijdagOnline.ImageResizer])
