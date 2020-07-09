using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ggm_troyedition
{
    bool all(bool[] x)       // bvec can be bvec2, bvec3 or bvec4
    {
        bool result = true;
        int i;
        for (i = 0; i < x.Length; ++i)
        {
            result &= x[i];
        }
        return result;
    }

    bool any(bool[] x)
    {     // bvec can be bvec2, bvec3 or bvec4
        bool result = false;
        int i;
        for (i = 0; i < x.Length; ++i)
        {
            result |= x[i];
        }
        return result;
    }

    Vector3 sum_vec3(Vector3 input_vector)
    {
        float sum = input_vector.x + input_vector.y + input_vector.z;
        return new Vector3(sum, sum, sum);
    }

    Vector3 max_vec3(Vector3 input_vector)
    {
        float max = Mathf.Max(Mathf.Max(input_vector.x, input_vector.y), input_vector.z);
        return new Vector3(max, max, max);
    }

    bool[] greaterThan(Vector3 vecA, Vector3 vecB)
    {
        return new bool[3] { (vecA.x > vecB.x), (vecA.y > vecB.y), (vecA.z > vecB.z) };
    }

    bool[] lessThanEqual(Vector3 vecA, Vector3 vecB)
    {
        return new bool[3] { (vecA.x <= vecB.x), (vecA.y <= vecB.y), (vecA.z <= vecB.z) };
    }

    // The dumbest and most direct approach to volumetric gamut
    // mapping you will ever see. Basically let a channel reach
    // peak value and spill over in a completely ignorant fashion
    // holding ratios.
    public Vector3 the_ggm(Vector3 input_rgb, Vector3 threshold)
    {
        // Escape out if all values are beyond the GGM threshold
        // to prevent negative value checks and clamps below.
        if (all(greaterThan(input_rgb, threshold)))
        {
            return input_rgb;
        }

        // Skip any values that are within the GGM threshold range.
        if (any(greaterThan(input_rgb, threshold)))
        {
            // Generate a 0.0 or 1.0 mask for every channel over the
            // GGM threshold. These are the channels that will require
            // having energy removed
            bool[] res = greaterThan(input_rgb, threshold);
            Vector3 over_energy_mask = new Vector3(Convert.ToSingle(res[0]), Convert.ToSingle(res[1]), Convert.ToSingle(res[2]));

            // Generate a 0.0 or 1.0 mask for every channel under the
            // GGM threshold. These are the channels that will require
            // receiving the excess energy.
            res = lessThanEqual(input_rgb, threshold);
            Vector3 under_energy_mask = new Vector3(Convert.ToSingle(res[0]), Convert.ToSingle(res[1]), Convert.ToSingle(res[2]));

            // Sum how much energy we are redistributing. This is
            // any energy over and above the threshold. Negative
            // energies result for subtraction.
            Vector3 over_energies = new Vector3(over_energy_mask.x * (threshold.x - input_rgb.x), 
                                                over_energy_mask.y * (threshold.y - input_rgb.y),
                                                over_energy_mask.z * (threshold.z - input_rgb.z));

            // Sum the under energy wells. This is where the excess energy
            // will be distributed to. Positive energies result for
            // addition.
            Vector3 under_energies = new Vector3(under_energy_mask.x * (threshold.x - input_rgb.x), under_energy_mask.y * (threshold.y - input_rgb.y),
                                                 under_energy_mask.z * (threshold.z - input_rgb.z));

            // Calculate the total of energy beyond the threshold.
            Vector3 total_over = sum_vec3(new Vector3(Mathf.Abs(over_energies.x), Mathf.Abs(over_energies.y), Mathf.Abs(over_energies.z)));
            Vector3 over_ratio = new Vector3(over_energies.x / total_over.x, over_energies.y / total_over.y, over_energies.z / total_over.z);

            // Calculate the total of energy that can be redistributed to.
            Vector3 total_under = sum_vec3(under_energies);
            Vector3 under_ratio = new Vector3(under_energies.x / total_under.x, under_energies.y / total_under.y, under_energies.z / total_under.z);

            // Merge the masked values into a single vector.
            Vector3 under_over_merged = over_ratio + under_ratio;

            // Given that the energy over the threshold may sum to
            // more than the display volume, use a 0% and 100% clip.
            // Rather brute force and unnecessary given that the display
            // will clip the values implicitly as it cannot display greater
            // than 100% output for example, it's worth noting and
            // being explicit for the case of demonstration.
            input_rgb += new Vector3(Mathf.Clamp(total_over.x * under_over_merged.x, 0.0f, 1.0f), 
                                     Mathf.Clamp(total_over.y * under_over_merged.y, 0.0f, 1.0f),
                                     Mathf.Clamp(total_over.z * under_over_merged.z, 0.0f, 1.0f));
        }

        return input_rgb;
    }
}
