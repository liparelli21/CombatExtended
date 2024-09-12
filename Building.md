# Building Instructions
If you want to build CE yourself, pick your platform and preferred build system and follow the instructions.

## Linux
### Make.py
This is the build system used for the CI system on github.  It does not execute arbitrary code from `.csproj` files, which makes it suitable for building PRs with repository secrets.  
It requires `python3.6` or later, `wget`, and `mono` (a recent enough version to have the Roslyn (`csc`) compiler).  

You can get the full usage string via `python3 Make.py --help`.  
If you want to use it to generate publicized assemblies (required starting with CE for RW 1.3), you need to check out the AssemblyPublicizer ( https://github.com/CombatExtended-Continued/AssemblyPublicizer )
and tell Make.py where to find it.

The invocation used by CI is about as simple as it gets.
`python Make.py --all-libs --download-libs` (for RW < 1.3), or
`python Make.py --download-libs --all-libs --publicizer=$PWD/AssemblyPublicizer` (for RW >= 1.3)

## Windows

### Rider or Visual Studio
To install and open in either Rider or Visual Studio.
```
$ git clone https://github.com/CombatExtended-Continued/CombatExtended
$ start CombatExtended/Source/CombatExtended.sln 
```
`start` will open the sln file with whatever IDE you have set to open .sln files by default. You can also just open the sln file normally.

After this, building is just building the solution, references will be pulled from Nuget, and the assemblies should be automatically publicised by the msbuild task. The resulting assembly will be in `$(root)/Assemblies/CombatExtended.dll`.

### Dotnet Build

If you want to compile without an IDE, you can clone the repo as usual, and then manually call `dotnet build CombatExtended.sln` from the `Source` directory.  

This can be automated by a GPL3 windows Batch file available [here](https://raw.githubusercontent.com/CombatExtended-Continued/CE-AutoInstaller-Updater-Builder/main/CE-AutoInstaller-Updater-Builder.bat).  
Simply download the file to your Rimworld/Mods directory and run it.  Running it again will rebase your local copy to the latest upstream and rebuild it.

This assumes you have `git` and `dotnet` configured to be available from windows batch files.  See the readme file included in its repository for details.

The repository for it is https://github.com/CombatExtended-Continued/CE-AutoInstaller-Updater-Builder 

## Other options


If you have a build system not listed here, please document how to use it.
