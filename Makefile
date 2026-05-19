run:
	DOTNET_GCHeapHardLimit=7C0000000 dotnet run --project cli

build:
	DOTNET_GCHeapHardLimit=7C0000000 dotnet build

format:
	DOTNET_GCHeapHardLimit=7C0000000 dotnet format

publish:
	dotnet publish \
	  -c Release \
	  -r linux-arm64 \
	  --self-contained true \
	  -p:PublishSingleFile=true \
	  -p:StripSymbols=true \
	  -p:InvariantGlobalization=true \
	  -p:DebugType=None \
	  -p:DebugSymbols=false \
	  -o ./bin \
	  cli
	@echo "Run with: ./bin/cli"
