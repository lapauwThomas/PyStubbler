# Build python stubs for .NET assemblies

Forked from [McNeel python stubs](https://github.com/mcneel/pythonstubs). So kudos to him for doing the heavy lifting. 

If any issues arise I might try and fix them. However no promises are made

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
