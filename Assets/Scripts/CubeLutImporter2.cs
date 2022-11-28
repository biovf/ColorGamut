﻿using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using UnityEngine;


[UnityEditor.AssetImporters.ScriptedImporter(0, "cube")]
public class CubeLutImporter2 : UnityEditor.AssetImporters.ScriptedImporter
{
    public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext ctx)
    {
        string filename = Path.GetFileNameWithoutExtension(ctx.assetPath);
        
        bool success = ParseCubeData(ctx, out int lutSize, out Color[] pixels);
        if (!success)
            return;

        var tex = new Texture3D(lutSize, lutSize, lutSize, TextureFormat.RGBAHalf, false)
        {
            // @TODO enable trilinear
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0
        };
        tex.SetPixels(pixels);

        ctx.AddObjectToAsset("3D Lookup Texture", tex);
        ctx.SetMainObject(tex);
    }

    // Trim useless spaces and remove comments
    string FilterLine(string line)
    {
        var filtered = new StringBuilder();
        line = line.TrimStart().TrimEnd();
        int len = line.Length;
        int o = 0;

        while (o < len)
        {
            char c = line[o];
            if (c == '#') break; // Comment filtering
            filtered.Append(c);
            o++;
        }

        return filtered.ToString();
    }

    bool ParseCubeData(UnityEditor.AssetImporters.AssetImportContext ctx, out int lutSize, out Color[] pixels)
    {
        // Quick & dirty error utility
        bool Error(string msg)
        {
            ctx.LogImportError(msg);
            return false;
        }

        var lines = File.ReadAllLines(ctx.assetPath);
        pixels = null;
        lutSize = -1;

        // Start parsing
        int sizeCube = -1;
        var table = new List<Color>();

        for (int i = 0; true; i++)
        {
            // EOF
            if (i >= lines.Length)
            {
                if (table.Count != sizeCube)
                    return Error("Premature end of file");

                break;
            }

            // Cleanup & comment removal
            var line = FilterLine(lines[i]);

            if (string.IsNullOrEmpty(line))
                continue;

            // Header data
            if (line.StartsWith("TITLE"))
                continue; // Skip the title tag, we don't need it

            if (line.StartsWith("LUT_3D_SIZE"))
            {
                var sizeStr = line.Substring(11).TrimStart();

                if (!int.TryParse(sizeStr, out var size))
                    return Error($"Invalid data on line {i}");

                // if (size < GlobalPostProcessSettings.k_MinLutSize || size > GlobalPostProcessSettings.k_MaxLutSize)
                //     return Error("LUT size out of range");

                lutSize = size;
                sizeCube = size * size * size;

                continue;
            }

            if (line.StartsWith("DOMAIN_"))
                continue; // Skip domain boundaries, haven't run into a single cube file that used them

            // Table
            var row = line.Split();

            if (row.Length != 3)
                return Error($"Invalid data on line {i}");

            var color = Color.black;

            for (int j = 0; j < 3; j++)
            {
                if (!float.TryParse(row[j], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out var d))
                    return Error($"Invalid data on line {i}");

                color[j] = d;
            }

            table.Add(color);
        }

        if (sizeCube != table.Count)
            return Error($"Wrong table size - Expected {sizeCube} elements, got {table.Count}");

        pixels = table.ToArray();
        return true;
    }
}