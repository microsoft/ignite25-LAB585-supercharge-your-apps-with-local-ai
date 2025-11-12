# AI Package Infrastructure Setup

## Overview

To integrate AI models into Windows applications, you must add packaging to provide them with a Windows identity. This is required for AI apps to access local AI models and Windows AI APIs.

**Key Points:**
- **WinAppSDK apps** are self-packaged and don't need external packaging
- **WPF, WinForms, and Console apps** are unpackaged and require an external packaging project

---

## Prerequisites

- **Visual Studio 2022** (17.8 or later)
- **.NET 9 SDK**
- **Windows 11** (Build 26100 or later)
- **Developer Mode enabled** in Windows Settings

---

We are creating a WinAppSDK application, so there is no need to run Step 1, which is needed only to non packaged apps. In this case, you can skip to Step 2.

## Step 1: Add Windows Application Packaging Project

1. **Add the packaging project:**
   - Right-click your solution in Solution Explorer
   - Select **Add** > **New Project**
   - Choose **Windows Application Packaging Project**
   - Leave the target and minimum platform versions as default
   - Click **OK**

2. **Set as startup project:**
   - In Solution Explorer, right-click the packaging project
   - Select **Set as Startup Project** (it should appear in bold)

3. **Add project reference:**
   - Right-click the **Dependencies** node in the packaging project
   - Select **Add Project Reference**
   - Check the box that references the main project and click **OK**

---

## Step 2: Configure the Package Manifest

To run local AI models, the `Package.appxmanifest` must specify the `systemAIModels` capability.

1. **Open the manifest editor:**
   - In Solution Explorer, double-click the **Package.appxmanifest** node
   - Press **F7** to open the XML editor

2. **Replace the entire content** with this code:

```xml
<!-- Declare systemai namespace and add it to IgnorableNamespaces -->
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  xmlns:systemai="http://schemas.microsoft.com/appx/manifest/systemai/windows10"
  IgnorableNamespaces="uap rescap systemai">

  <Identity
    Name="452e05ea-3445-4320-94a1-094e21a0bc0a"
    Publisher="CN=brunosonnino"
    Version="1.0.0.0" />

  <Properties>
    <DisplayName>ContosoLab.Package</DisplayName>
    <PublisherDisplayName>brunosonnino</PublisherDisplayName>
    <Logo>Images\StoreLogo.png</Logo>
  </Properties>

  <Dependencies>
    <!-- Updated versions to support Windows AI APIs -->
    <TargetDeviceFamily Name="Windows.Universal" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26226.0" />
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.17763.0" MaxVersionTested="10.0.26226.0" />
  </Dependencies>

  <Resources>
    <Resource Language="x-generate"/>
  </Resources>

  <Applications>
    <Application Id="App"
      Executable="$targetnametoken$.exe"
      EntryPoint="$targetentrypoint$">
      <uap:VisualElements
        DisplayName="ContosoLab.Package"
        Description="ContosoLab.Package"
        BackgroundColor="transparent"
        Square150x150Logo="Images\Square150x150Logo.png"
        Square44x44Logo="Images\Square44x44Logo.png">
        <uap:DefaultTile Wide310x150Logo="Images\Wide310x150Logo.png" />
        <uap:SplashScreen Image="Images\SplashScreen.png" />
      </uap:VisualElements>
    </Application>
  </Applications>

  <Capabilities>
    <Capability Name="internetClient" />
    <rescap:Capability Name="runFullTrust" />
    <!-- Required capability for accessing local AI models -->
    <systemai:Capability Name="systemAIModels"/> 
  </Capabilities>
</Package>
```

**Key Changes Made:**
- Added `systemai` namespace declaration
- Included `systemai` in `IgnorableNamespaces`
- Added `systemAIModels` capability for AI model access
- Updated target device family versions for Windows AI API compatibility

---

## Step 3: Configure Project Settings

1. **Edit the project file:**
   - In Solution Explorer, right-click the **Package** project node, for unpackaged apps or the main project file, for WinAppSDK apps
   - Select **Edit Project File**
   - Add these two lines after the `EntryPointProjectUniqueName` tag (for unpackaged apps) or in the first `PropertyGroup` tag (for WinAppSdk apps):

```xml
<AppxOSMinVersionReplaceManifestVersion>false</AppxOSMinVersionReplaceManifestVersion>
<AppxOSMaxVersionTestedReplaceManifestVersion>false</AppxOSMaxVersionTestedReplaceManifestVersion>
```

These settings prevent Visual Studio from overriding the manifest version numbers.

---

## Step 4: Configure Platform Settings

AI models require specific CPU architectures and cannot run on `Any CPU`.

### 4.1 Configure Solution Platforms

1. **Open Configuration Manager:**
   - Right-click the solution node in Solution Explorer
   - Select **Properties**
   - Go to **Configuration Properties**
   - Click **Configuration Manager**

2. **Remove unsupported platforms:**
   - In **Active Solution Platform**, select **Edit**
   - Remove all platforms except `ARM64` and `x64`
   - Click **OK**

3. **Set the appropriate platform:**
   - Select `ARM64` or `x64` based on your machine architecture
   - To check your architecture: Press `Win+X` and select **System**

### 4.2 Configure Project Platforms

1. **Add platforms to ContosoLab project:**
   - In the **ContosoLab Platform** combo box, select **<New...>**
   - Add both `ARM64` and `x64` platforms
   - Select **<Edit...>** and remove `Any CPU`
   - Ensure the **Build** checkbox is checked

### 4.3 Update Target OS Version

1. **Set Target OS Version:**
   - Right-click the main project node in Solution Explorer
   - Select **Properties**
   - Change **Target OS Version** to `10.0.26100.0`

---

## Step 5: Add Required NuGet Package

1. **Install AI package:**
   - Right-click the **Dependencies** node in the main project
   - Select **Manage NuGet Packages**
   - Go to the **Browse** tab
   - Search for and install the `Microsoft.WindowsAppSDK` package. Choose version 2.0-Experimental2 or newer (the `Include Prerelease` box should be checked)

2. **Verify installation:**
   - Build the project (`Ctrl+Shift+B`)
   - Run the project to verify everything works

---

## Step 6: Test the Configuration

1. **Run the application:**
   - Select the correct platform (`ARM64` or `x64`)
   - Press **F5** to run

2. **Verify success:**
   - The app should launch with the same UI as before
   - The app is now packaged and can be launched from the Start menu
   - AI capabilities should be available

---

## Troubleshooting

### Common Issues and Solutions

**Error: "Assets file doesn't have a target for 'net9.0-windows10.0.26100.0'"**
- **Solution:** Rebuild the solution (`Build` > `Rebuild Solution`) and run again

**Error: Project doesn't compile or isn't properly rebuilt**
- **Solution:** 
  1. Right-click solution node > **Properties**
  2. Check that both projects are set to the correct platform
  3. Verify the **Build** checkbox is checked for ContosoLab

**Error: "Access Denied" when running**
- **Solution:** Verify the packaging project is set as the startup project (appears in bold)
- This error occurs when ContosoLab is set as the startup project instead

**Error: Platform not found**
- **Solution:** Ensure you've added the correct platform (`ARM64` or `x64`) to both projects

---

## Summary

After completing these steps, your application will have:

- ✅ **Windows Identity** - Required for AI model access
- ✅ **Proper Packaging** - Enables deployment and Start menu integration
- ✅ **AI Capabilities** - Access to local Windows AI models
- ✅ **Platform Configuration** - Optimized for AI workloads

Your application is now ready to integrate Windows AI APIs and local AI models!