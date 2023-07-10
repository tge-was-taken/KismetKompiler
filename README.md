# KismetKompiler

KismetKompiler is a powerful decompiler and compiler tool designed specifically for Unreal Engine 4 blueprints. It provides support for decompiling most blueprints into a C#-like syntax and offers the ability to compile the decompiled code using a custom script format called KisMetScript (.kms). 

## Features

- **Decompilation**: KismetKompiler can decompile most blueprints, converting them into a C#-like syntax. However, please note that not all blueprint constructs are currently supported.
- **Verification**: The tool automatically verifies the equality of the decompiled code to ensure accuracy during the decompilation process. This can be toggled off however.
- **Compilation**: KismetKompiler supports compiling the decompiled blueprint code using the .kms script format (KisMetScript).
- **Editing**: The tool provides limited editing functionality for decompiled code, allowing you to modify existing logic, add new logic, add new variables and functions, and import new library functions. However, certain actions, such as creating a class from scratch or modifying existing variable or function definitions, are not yet supported.
- **Error Detection**: The compiler includes limited error detection capabilities. However, please note that error messages might not be as descriptive as they should be in some cases.
- **Compatibility**: KismetKompiler has been primarily tested with Unreal Engine 4.23 assets, specifically those used in Shin Megami Tensei V.

## Installation

To use KismetKompiler, follow these steps:

1. Download the latest release.
2. Unzip the contents of the zip to a folder

## Usage

The general usage of KismetKompiler is as follows:

``KismetKompiler <command> [options]``

### ``help`` Command

Displays more information on a specific command.

``KismetKompiler help <command>``

### ``compile`` command

Compiles a script into a new or existing blueprint asset.

#### Options:

- `-i, --input`: (Required) Path to a .kms file containing the script to compile.
- `-o, --output`: Path to the output .uasset file. If not specified, a new blueprint asset will be created with a default name.
- `--asset`: Path to an existing .uasset file. If specified, the script will be compiled into this existing blueprint asset.
- `-v, --version`: (Optional) Unreal Engine version (e.g., 4.23).
- `-f, --overwrite`: (Optional) Overwrite existing files.
- `--usmap`: (Optional) Path to a .usmap file.
- `--help`: Display the help screen.
- `--version`: Display version information.

### ``decompile`` Command

Decompiles a blueprint asset into a .kms file.

#### Options:

- `-i, --input`: (Required) Path to an input .uasset file containing the blueprint asset to decompile.
- `-o, --output`: Path to the output .kms file. If not specified, a default name will be used.
- `--no-verify`: Skip verifying the equality of the decompiled code.
- `-v, --version`: (Optional) Unreal Engine version (e.g., 4.23).
- `-f, --overwrite`: (Optional) Overwrite existing files.
- `--usmap`: (Optional) Path to a .usmap file.
- `--help`: Display the help screen.
- `--version`: Display version information.

## Known Limitations

While KismetKompiler provides significant functionality for decompiling and compiling blueprints, it has a few limitations:

- Not all blueprint constructs are currently supported during decompilation, and will be expressed using so called intrinsic functions (prefixed with EX_...).
- The editing functionality is limited and does not support certain operations, such as creating a class from scratch or modifying existing variable or function definitions.
- Error messages may not be as descriptive as desired due to limited error detection capabilities.

## Contributing

Contributions to KismetKompiler are highly appreciated. If you encounter any issues, have feature requests, or would like to contribute to the project, please feel free to do so. You can submit bug reports, feature requests, or pull requests through the GitHub repository.

## Third-party

This project relies on the following third-party dependencies:

- **UAssetAPI** by atenfyr: A library that provides functionality for working with Unreal Engine .uasset files. This project would not be possible without it. [GitHub repository](https://github.com/atenfyr/UAssetAPI) - [MIT License](https://opensource.org/licenses/MIT)

## License

KismetKompiler is released under the [MIT License](https://opensource.org/licenses/MIT). See the `LICENSE` file for more details.
