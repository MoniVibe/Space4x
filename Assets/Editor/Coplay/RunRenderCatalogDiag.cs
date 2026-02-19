using UnityEngine;

public static class RunRenderCatalogDiag
{
    public static string Execute()
    {
        Space4XWorldDiag.PrintRenderCatalogState();
        return "Invoked Space4XWorldDiag.PrintRenderCatalogState.";
    }
}
