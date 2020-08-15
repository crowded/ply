#! /bin/bash

PLY_NUGET_KEY=${PLY_NUGET_KEY?env var with nuget credentials is not set}
version="$(xmllint --xpath '//Project/PropertyGroup/Version/text()' Ply.fsproj)"

dotnet pack Ply.fsproj -c Release -p:ContinuousIntegrationBuild=true
dotnet nuget push "bin/Release/Ply.${version}.nupkg" -s nuget.org --interactive -k $PLY_NUGET_KEY

