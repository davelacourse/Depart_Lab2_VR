# StationBuilder — reconstruction de Station_VR

Scripts d'éditeur qui ont servi à construire `Assets/_MyAssets/Scenes/Station_VR.unity`.
Ils sont gardés **hors du projet Unity** : le projet remis aux étudiants n'en dépend pas.

## Reconstruire

1. Copier les fichiers `.cs` de ce dossier dans `Assets/_Builder/Editor/`.
2. Dans Unity, lancer dans l'ordre :
   - `Tools/Station/Create Materials` — matériaux URP Lit, conversion des textures ORM, remap des FBX Quaternius ;
   - `Tools/Station/Measure Modules` — mesures des modules (log) ;
   - `Tools/Station/Build Scene` — construit et sauvegarde la scène, met à jour la liste des scènes du build ;
   - `Tools/Station/Validate` — rapport de validation + captures dans `Screenshots/`.
3. Supprimer `Assets/_Builder/`.

## Mode batch (éditeur fermé sur ce projet)

```
"C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" -batchmode -projectPath <projet> -executeMethod StationMaterials.CreateMaterials -logFile mat.log -quit
"C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" -batchmode -projectPath <projet> -executeMethod StationBuilder.BuildScene -logFile build.log -quit
"C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" -batchmode -projectPath <projet> -executeMethod StationValidate.Validate -logFile validate.log -quit
```

Les rapports sont aussi écrits dans `Logs/Station_*.txt`.

## Fichiers

- `StationMaterials.cs` : matériaux (+ `StationLog`).
- `StationMeasure.cs` : mesure des modules et cache des chemins FBX.
- `StationBuilder.cs` : construction de la scène.
- `StationValidate.cs` : validations et captures.
- `StationCapture.cs` : caméra temporaire rendue dans une RenderTexture.
- `Rapport_*.txt` : derniers rapports produits.
