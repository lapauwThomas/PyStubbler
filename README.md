# Build python stubs for .NET assemblies

Forked from [McNeel python stubs](https://github.com/mcneel/pythonstubs). So kudos to him for doing the initial work. 

The motivation behind this is to be able to generate the python stubs that can be used in combination with Pythonnet.


## Usage
```
Usage:
    PyStubbler (-h | --help)
    PyStubbler (-V | --version)
    PyStubbler [--dest=<dest_path>] [--search=<search_path>...] [--prefix=<prefix>] [--postfix=<postfix>] [--dest-is-root] <target_dll>...

Options:
    -h --help                   Show this help
    -V --version                Show version
    --dest=<dest_path>          Path to save the subs to
    --search=<search_path>      Path to search for referenced assemblies. Can be repeated multiple times for multiple paths
    --prefix=<prefix>           Root namespace directory prefix
    --postfix=<postfix>         Root namespace directory postfix [default: -stubs]
    --dest-is-root              Use destination path for root namespace
```

## Example
 ` .\PyStubbler.exe --dest=".\path\to\output" --search=".\path\to\referenced\assemblies\" --postfix="-stubs" ".\path\to\target.dll"`
 
 
## Visual Studio postbuild

Place the all the files from the release folder in a subfolder called _PyStubbler_ in the solution folder, and add the following line to the Post-build event command line box in the project properties

`"$(SolutionDir)PyStubbler\PyStubbler.exe" --dest="$(TargetDir)" --search="$(TargetDir)" --postfix="-stubs" "$(TargetPath)"`

## The following changes and improvements have been made:
- Creation of the `[HideStub]` attribute in _PyStubblerAnnotations.dll_. This attribute hides class members from the stub generator.
- If the destination folder does not exists, it gets created.
- Public fields are added as well.
- Static accessors are marked as such using `@staticmethod`
- Static fields are listed separately in the stub file.
- Improved type conversion especially for nested types. (WIP)
- Added support for dictionary typing.
- Properties are marked separately
- Added eventhandler support. Marked as _Any_ with comment for type.
- Added logging with NLOG to the library. 

## Future improvements (when I need them or get around to it)
- Refactor the code for better maintainability
- Write tests




## Known limitations
- Spaces on paths are tricky to get right when running the tool. 
- Figure out how to do event properties properly