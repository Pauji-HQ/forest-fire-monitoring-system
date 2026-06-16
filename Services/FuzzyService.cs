using System;

namespace APP.Services;

public class FuzzyService
{
    private static double AreaKecil(double x)
    {
        if (x <= 10) return 1.0;
        if (x > 10 && x < 30) return (30.0 - x) / (30.0 - 10.0);
        return 0.0;
    }

    private static double AreaSedang(double x)
    {
        if (x <= 15 || x >= 55) return 0.0;
        if (x > 15 && x <= 35) return (x - 15.0) / (35.0 - 15.0);
        return (55.0 - x) / (55.0 - 35.0);
    }

    private static double AreaBesar(double x)
    {
        if (x <= 45) return 0.0;
        if (x > 45 && x < 65) return (x - 45.0) / (65.0 - 45.0);
        return 1.0;
    }

    private static double WindLambat(double x)
    {
        if (x <= 15) return 1.0;
        if (x > 15 && x < 35) return (35.0 - x) / (35.0 - 15.0);
        return 0.0;
    }

    private static double WindSedang(double x)
    {
        if (x <= 20 || x >= 70) return 0.0;
        if (x > 20 && x <= 45) return (x - 20.0) / (45.0 - 20.0);
        return (70.0 - x) / (70.0 - 45.0);
    }

    private static double WindCepat(double x)
    {
        if (x <= 55) return 0.0;
        if (x > 55 && x < 75) return (x - 55.0) / (75.0 - 55.0);
        return 1.0;
    }

    public double CalculateDangerIndex(double areaPercent, double windSpeed)
    {
        double muAreaKecil = AreaKecil(areaPercent);
        double muAreaSedang = AreaSedang(areaPercent);
        double muAreaBesar = AreaBesar(areaPercent);

        double muWindLambat = WindLambat(windSpeed);
        double muWindSedang = WindSedang(windSpeed);
        double muWindCepat = WindCepat(windSpeed);

        double z1 = 15.0;  
        double z2 = 35.0;  
        double z3 = 60.0;  
        double z4 = 40.0;  
        double z5 = 65.0;  
        double z6 = 85.0;  
        double z7 = 70.0;  
        double z8 = 90.0;  
        double z9 = 100.0; 

        double[] w = new double[9];
        w[0] = Math.Min(muAreaKecil, muWindLambat);
        w[1] = Math.Min(muAreaKecil, muWindSedang);
        w[2] = Math.Min(muAreaKecil, muWindCepat);

        w[3] = Math.Min(muAreaSedang, muWindLambat);
        w[4] = Math.Min(muAreaSedang, muWindSedang);
        w[5] = Math.Min(muAreaSedang, muWindCepat);

        w[6] = Math.Min(muAreaBesar, muWindLambat);
        w[7] = Math.Min(muAreaBesar, muWindSedang);
        w[8] = Math.Min(muAreaBesar, muWindCepat);

        double[] z = [z1, z2, z3, z4, z5, z6, z7, z8, z9];

        double sumW = 0;
        double sumWZ = 0;

        for (int i = 0; i < 9; i++)
        {
            sumW += w[i];
            sumWZ += w[i] * z[i];
        }

        if (sumW == 0) return 10.0;

        return sumWZ / sumW;
    }
}