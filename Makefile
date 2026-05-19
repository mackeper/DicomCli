run:
	DOTNET_GCHeapHardLimit=7C0000000 dotnet run --project cli

build:
	DOTNET_GCHeapHardLimit=7C0000000 dotnet build

format:
	DOTNET_GCHeapHardLimit=7C0000000 dotnet format
