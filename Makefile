# Copyright The ORAS Authors.
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
# http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

CONFIGURATION=Release

# Function to determine the version from Git and generate a SemVer 2.0 compliant version.
# Ensure you git tag with -a to annotate the tag with a message
get_version = $(shell \
	TAG=$$(git describe --tags) && \
	echo $$TAG | sed 's/^v//'; \
)

build:
	dotnet build ./src/OrasProject.Oras --configuration $(CONFIGURATION)

test:
	dotnet test ./tests/OrasProject.Oras.Tests --configuration $(CONFIGURATION) 

pack:
	$(eval VERSION := $(call get_version))
	echo $(VERSION)
	@if [ -z "$(VERSION)" ]; then \
		dotnet pack ./src/OrasProject.Oras --configuration $(CONFIGURATION) --no-build -o nupkgs; \
	else \
		dotnet pack ./src/OrasProject.Oras --configuration $(CONFIGURATION) --no-build -o nupkgs /p:PackageVersion=$(VERSION); \
	fi

clean:
	dotnet clean ./src/OrasProject.Oras
	dotnet clean ./tests/OrasProject.Oras.Tests

.PHONY: build test pack publish clean
