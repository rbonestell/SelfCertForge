MAC_FRAMEWORK := net10.0-maccatalyst
WIN_FRAMEWORK := net10.0-windows10.0.19041.0
PROJECT := SelfCertForge.App/SelfCertForge.App.csproj

ifeq ($(OS),Windows_NT)
	SHELL := cmd.exe
	.SHELLFLAGS := /C
	FRAMEWORK := $(WIN_FRAMEWORK)
	RM_BIN := if exist SelfCertForge.App\bin rmdir /S /Q SelfCertForge.App\bin
	RM_OBJ := if exist SelfCertForge.App\obj rmdir /S /Q SelfCertForge.App\obj
	KILL_APP := -taskkill /F /IM SelfCertForge.exe >NUL 2>&1
else
	FRAMEWORK := $(MAC_FRAMEWORK)
	RM_BIN := rm -rf SelfCertForge.App/bin
	RM_OBJ := rm -rf SelfCertForge.App/obj
	KILL_APP := -pkill -x SelfCertForge
endif

.PHONY: build clean rebuild run kill test

build:
	dotnet build $(PROJECT) -f $(FRAMEWORK)

clean:
	dotnet clean $(PROJECT) -f $(FRAMEWORK)
	$(RM_BIN)
	$(RM_OBJ)

rebuild: clean build

kill:
	$(KILL_APP)

run: kill build
	dotnet run --project $(PROJECT) -f $(FRAMEWORK)

test:
	dotnet test
