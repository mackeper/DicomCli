VERSION ?= 0.1.0

run:
	DOTNET_GCHeapHardLimit=7C0000000 dotnet run --project src/cli

build:
	DOTNET_GCHeapHardLimit=7C0000000 dotnet build

format:
	DOTNET_GCHeapHardLimit=7C0000000 dotnet format

test:
	DOTNET_GCHeapHardLimit=7C0000000 dotnet test --solution DicomCli.slnx --configuration Release

check:
	DOTNET_GCHeapHardLimit=7C0000000 dotnet restore DicomCli.slnx
	DOTNET_GCHeapHardLimit=7C0000000 dotnet format DicomCli.slnx --verify-no-changes --no-restore
	DOTNET_GCHeapHardLimit=7C0000000 dotnet build DicomCli.slnx --configuration Release --no-restore
	DOTNET_GCHeapHardLimit=7C0000000 dotnet test --solution DicomCli.slnx --configuration Release --no-build

publish:
	rm -f ./bin/cli ./bin/cli.exe
	DOTNET_GCHeapHardLimit=7C0000000 dotnet publish \
	  -c Release \
	  -r linux-arm64 \
	  --self-contained true \
	  -p:PublishSingleFile=true \
	  -p:Version=$(VERSION) \
	  -p:InformationalVersion=$(VERSION) \
	  -p:StripSymbols=true \
	  -p:InvariantGlobalization=true \
	  -p:DebugType=None \
	  -p:DebugSymbols=false \
	  -o ./bin \
	  src/cli
	@echo "Run with: ./bin/dicomcli"
