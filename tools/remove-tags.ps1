function Remove-TagOnResourceGroup {
    param (
        [Parameter(Mandatory=$true)]
        [string] $TagName
    )
    
    Get-AzResourceGroup | ForEach-Object {
        if ( $_.tags.ContainsKey($TagName) ) {
            $_.tags.Remove($TagName)
        }
        $_ | Set-AzResource -Tags $_.tags
    }
}
