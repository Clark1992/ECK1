param (
    [string]$solutionDir = "src",
    [string]$solutionFile = "ECK1.sln",
    [string]$schemaRoot   = "_SolutionItems/SchemaRegistry"
)

try 
{
    Push-Location $solutionDir

    Write-Host "Updating solution file: $solutionDir/$solutionFile with schema root: $solutionDir/$schemaRoot"

    $schemaFolder = Split-Path $schemaRoot -Leaf

    # Read solution file
    $solution = Get-Content $solutionFile -Raw -Encoding UTF8
    $solutionOriginal = $solution

    # Regex for Project blocks
    $projectRegex = 'Project\("\{[^\}]+\}"\) = "(?<name>[^"]+)", "(?<path>[^"]+)", "\{(?<guid>[^\}]+)\}"'
    $matches = [regex]::Matches($solution, $projectRegex)

    # Write-Host "Project matches:"
    # Write-Host $matches

    # 1. Add solution folders

    # Dictionary of existing projects
    $projects = @{}
    foreach ($m in $matches) {
        $projects[$m.Groups['path'].Value] = @{
            Name = $m.Groups['name'].Value
            Guid = $m.Groups['guid'].Value
        }
    }

    # Write-Host "Existing projects in solution Dict:"
    # Write-Host $projects

    # Regex for NestedProjects mappings
    $nestedRegex = '\{(?<child>[^\}]+)\} = \{(?<parent>[^\}]+)\}'
    $nestedMatches = [regex]::Matches($solution, $nestedRegex)

    # Write-Host "Nested matches:"
    # Write-Host $nestedMatches

    $nested = @{}
    foreach ($nm in $nestedMatches) {
        $nested[$nm.Groups['child'].Value] = $nm.Groups['parent'].Value
    }

    # Write-Host "Nested Projects Dict:"
    # Write-Host $nested

    function New-SolutionGuid {
        return [guid]::NewGuid().ToString().ToUpper()
    }

    # Write-Host $projects.ContainsKey($schemaFolder)

    # Ensure schema root exists
    if (-not ($projects.ContainsKey($schemaFolder))) {
        $guid = New-SolutionGuid
        Write-Host "Adding root folder: $schemaFolder ($guid)"
        $projBlock = "Project(`"{2150E333-8FDC-42A3-9474-1A3956D46DE8}`") = `"SchemaRegistry`", `"$schemaFolder`", `"$guid`"`r`nEndProject"
        $solution = $solution -replace "(?s)(?=Global)", "$projBlock`r`n"
        $projects[$schemaFolder] = @{ Name = "SchemaRegistry"; Guid = $guid }
    }

    # Walk folders
    $folders = Get-ChildItem $schemaRoot -Recurse -Directory

    # Write-Host "Found folders in schema root:"
    # Write-Host $folders

    foreach ($folder in $folders) {
        $root = (Get-Location).Path
        $relPath = [System.IO.Path]::GetRelativePath($root, $folder.FullName).Replace("/", "\")
        $relFolder = Split-Path $relPath -Leaf
        
        # Write-Host "Relative path:"
        # Write-Host $relPath

        # Write-Host "Relative path:"
        # Write-Host $relFolder
        # Write-Host $projects.ContainsKey($relFolder)

        if (-not $projects.ContainsKey($relFolder)) {
            $guid = New-SolutionGuid
            Write-Host "Adding folder: $relPath ($guid)"
            $projBlock = "Project(`"{2150E333-8FDC-42A3-9474-1A3956D46DE8}`") = `"$relFolder`", `"$relFolder`", `"{$guid}`"`r`nEndProject"
            $index = $solution.IndexOf('Global')

            if ($index -ge 0) {
                $before = $solution.Substring(0, $index)
                $after = $solution.Substring($index)
                $solution = "$before$projBlock`r`n$after"
            }
            
            $projects[$relFolder] = @{ Name = $relFolder; Guid = $guid }
        }

        # Add mapping if not already exists
        $parentPath = Split-Path $relPath -Parent
        $parentFolder = Split-Path $parentPath -Leaf

        # Write-Host "Parent Folder:"
        # Write-Host $parentFolder

        if ($projects.ContainsKey($parentFolder)) {
            $parentGuid = $projects[$parentFolder].Guid
            $childGuid  = $projects[$relFolder].Guid

            # Write-Host "Parent Guid:"
            # Write-Host $parentGuid
            
            # Write-Host "Child Guid:"
            # Write-Host $childGuid

            if (-not $nested.ContainsKey($childGuid)) {
                Write-Host "Adding mapping $childGuid -> $parentGuid"
                $insert = "`t`t{$childGuid} = {$parentGuid}"
                $solution = $solution -replace '(?s)(GlobalSection\(NestedProjects\).*?)(\s*)(EndGlobalSection)', "`$1`r`n$insert`$2`$3"
                $nested[$childGuid] = $parentGuid
            }
        }
    }

    # 2. Add files
    $files = Get-ChildItem $schemaRoot -Recurse -File

    # Write-Host "Found files in schema root:"
    # Write-Host $files

    foreach ($file in $files) {
        $root = (Get-Location).Path
        $relPath = [System.IO.Path]::GetRelativePath($root, $file.FullName).Replace("/", "\")
        $folderPath  = Split-Path $relPath -Parent

        $folderProjectKey  = Split-Path $folderPath -leaf

        # Write-Host "file relpath:"
        # Write-Host $relPath

        # Write-Host "BEFORE: "
        # Write-Host $solution

        if ($projects.ContainsKey($folderProjectKey)) {
            $folderGuid = $projects[$folderProjectKey].Guid

            # Write-Host "folderGuid:"
            # Write-Host $folderGuid
            # Skip if file already in solution
            if ($solution -notmatch [regex]::Escape($relPath)) {
                Write-Host "Adding file: $relPath"

                $pattern = "(?s)Project\(`"\{2150E333-8FDC-42A3-9474-1A3956D46DE8\}`"\) = `"$folderProjectKey`", `"$folderProjectKey`", `"{$folderGuid}`"(.*?)(?<!EndProjectSection)(?<!\w)EndProject(?!\w)"
                $insert = "`tProjectSection(SolutionItems) = preProject`r`n`t`t$relPath = $relPath`r`n`tEndProjectSection"                    
                $replacement = "Project(`"{2150E333-8FDC-42A3-9474-1A3956D46DE8}`") = `"$folderProjectKey`", `"$folderProjectKey`", `"{$folderGuid}`"`$1$insert`r`nEndProject"
                
                $match = [regex]::Matches($solution, $pattern)[0]
                
                $projectBody = $match.Groups[1].Value

                # Write-Host "projectBody:"
                # Write-Host $projectBody

                # If ProjectSection already exists, append to it instead
                if ($projectBody -match 'ProjectSection\(SolutionItems\)') {
                    $pattern = "(?s)Project\(`"\{2150E333-8FDC-42A3-9474-1A3956D46DE8\}`"\) = `"$folderProjectKey`", `"$folderProjectKey`", `"{$folderGuid}`"(.*?)EndProjectSection`r`nEndProject"
                    $insert = "`t`t$relPath = $relPath"
                    $replacement = "Project(`"{2150E333-8FDC-42A3-9474-1A3956D46DE8}`") = `"$folderProjectKey`", `"$folderProjectKey`", `"{$folderGuid}`"`$1$insert`r`n`tEndProjectSection`r`nEndProject"
                }        

                # Write-Host "Pattern:"
                # Write-Host $pattern

                # Write-Host "Replacement:"
                # Write-Host $replacement

                # Write-Host "Insert:"
                # Write-Host $insert

                $solution = [regex]::Replace($solution, $pattern, $replacement, "Singleline")

                # Write-Host "AFTER: "
                # Write-Host $solution
                # Write-Host $solution
            }
        }
    }

        # Write-Host "Items:"
        # Write-Host $itemsBlock

    # 3. Cleanup missing files from SchemaRegistry only
    Write-Host "Cleaning up missing files in SchemaRegistry..."

    $solutionItemsRegex = '(?ms)ProjectSection\(SolutionItems\) = preProject\s*(?<items>.*?)(\s*)EndProjectSection'
    $solution = [regex]::Replace($solution, $solutionItemsRegex, {
        param($match)

        $itemsBlock = $match.Groups['items'].Value

        # Split lines
        $lines = $itemsBlock -split "`r?`n"

        # Write-Host "lines"
        # Write-Host $lines

        $newLines = @()
        foreach ($line in $lines) {

            $trimLine = $line.Trim()
            $pathSection = $trimLine.Split('=')[0].Trim()

            # Write-Host "trimLine:"
            # Write-Host $trimLine

            if ($trimLine -like "$schemaRoot*") {
                # Keep only if file exists physically

                # Write-Host "Testing Path $pathSection"
                if (Test-Path $pathSection) {
                    # Write-Host "Exists"
                    $newLines += $trimLine
                }
            } else {
                # Keep line as-is (outside SchemaRegistry)
                $newLines += $trimLine
            }
        }

        if ($newLines.Count -gt 0) {
            $linesText = ($newLines | ForEach-Object { "`t`t$_" }) -join "`r`n"
            return "ProjectSection(SolutionItems) = preProject`r`n$linesText`r`n`tEndProjectSection"
        } else {
            return ""
        }
    })

    # 4. Cleanup empty Project sections (solution folders missing on disk)

    # Regex to find solution folder projects
    $matches = [regex]::Matches($solution, $projectRegex)

    # Write-Host "Project matches:"
    # Write-Host $matches

    # Dictionary of existing projects
    $projects = @{}
    foreach ($m in $matches) {
        $projects[$m.Groups['guid'].Value] = @{
            Name = $m.Groups['name'].Value
        }
    }

    $sectionText = [regex]::Match($solution, '(?s)GlobalSection\(NestedProjects\)\s*=\s*preSolution\s*(.*?)EndGlobalSection').Groups[1].Value

    $nestedProjects = @{}

    # Regex for pairs GUID = GUID
    $guidPattern = '^\s*\{(?<child>[^\}]+)\}\s*=\s*\{(?<parent>[^\}]+)\}'

    $sectionText -split "`n" | ForEach-Object {
        if ($_ -match $guidPattern) {
            $child = $matches['child']
            $parent = $matches['parent']
            $nestedProjects[$child] = $parent
        }
    }

    function Get-FolderPath($guid, $projects, $nested) {
        $entry = $projects[$guid]
        if (-not $entry) { return $null }

        $name = $entry.Name

        if ($nested.ContainsKey($guid)) {
            $parentGuid = $nested[$guid]
            $parentPath = Get-FolderPath $parentGuid $projects $nested
            if ($parentPath) {
                return "$parentPath\$name"
            }
        }

        return $name
    }

    function Remove-ProjectByGuid {
        param(
            [string]$solution,
            [string]$guid
        )

        # 1. Remove Project block
        $projectRegex = "(?ms)Project\(`"\{2150E333-8FDC-42A3-9474-1A3956D46DE8\}`"\) = `"[^`"]+`", `"[^`"]+`", `"\{$guid\}`".*?EndProject\r?\n"
        $solution = [regex]::Replace($solution, $projectRegex, "")

        # 2.Remove from NestedProjects
        $nestedRegex = "^\s*\{$guid\}\s*=\s*\{[A-F0-9\-]+\}\s*\r?\n"
        $solution = [regex]::Replace($solution, $nestedRegex, "", "IgnoreCase, Multiline")

        $nestedRegex2 = "^\s*\{[A-F0-9\-]+\}\s*=\s*\{$guid\}\s*\r?\n"
        $solution = [regex]::Replace($solution, $nestedRegex2, "", "IgnoreCase, Multiline")

        return $solution
    }

    $schemaRegistryGuid = ($projects.GetEnumerator() |
        Where-Object { $_.Value.Name -eq "SchemaRegistry" } |
        Select-Object -First 1 -ExpandProperty Key)

    $schemaRoot = Get-FolderPath $schemaRegistryGuid $projects $nestedProjects

    foreach ($nestedKVP in $nestedProjects.GetEnumerator()) {
        
        $childGuid = $nestedKVP.Key
        $projectFolderPath = Get-FolderPath $childGuid $projects $nestedProjects
        # Write-Host "$childGuid ($($projects[$childGuid].Name)) -> $projectFolderPath"

        if ($projectFolderPath -like "$schemaRoot*" -and $projectFolderPath -ne $schemaRoot) {
            Write-Host "Checking path: $projectFolderPath"
            if(-not (Test-Path $projectFolderPath)) {
                Write-Host "Removing project with GUID: $guid"
                $solution = Remove-ProjectByGuid -solution $solution -guid $childGuid
            }
        }
    }

    if ($solutionOriginal -eq $solution) {
        Write-Host "No changes to solution."
        exit 0
    }

    $cwd = Get-Location
    $fullPath = Join-Path $cwd "ECK1.sln"

    [System.IO.File]::WriteAllText($fullPath, $solution, [System.Text.Encoding]::UTF8)
    Write-Host "Solution updated successfully."
}
catch {
    Write-Error "An error occurred: $_"
    exit 1
}
finally {
    Pop-Location
}