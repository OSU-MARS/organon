﻿using Mars.Seem.Extensions;
using System;
using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Avx512 = System.Runtime.Intrinsics.X86.Avx10v1.V512;

namespace Mars.Seem.Tree
{
    public static class DouglasFir
    {
        public static TreeSpeciesProperties Properties { get; private set; }

        static DouglasFir()
        {
            // Miles PD, Smith BW. 2009. Specific gravity and other properties of wood and bark for 156 tree species found in North
            //   America (No. NRS-RN-38). Northern Research Station, US Forest Service. https://doi.org/10.2737/NRS-RN-38
            DouglasFir.Properties = new TreeSpeciesProperties(greenWoodDensity: 609.0F, // kg/m³
                barkFraction: 0.176F,
                barkDensity: 833.0F, // kg/m³
                processingBarkLoss: 0.30F, // loss with spiked feed rollers
                yardingBarkLoss: 0.15F); // dragging abrasion loss over full corridor (if needed, this could be reparameterized to a function of corridor length)
        }

        // Diameter outside bark at heights of 30 cm to 1.37 m from
        // Maguire DA, Hann DW. 1990. Bark Thickness and Bark Volume in Southwestern Oregon Douglas-Fir. Western Journal of Applied
        //   Forestry 5(1):5–8. https://doi.org/10.1093/wjaf/5.1.5
        // with extrapolation allowed to lower heights.
        public static float GetDiameterOutsideBark(float dbhInCm, float heightInM, float heightToCrownBaseInM, float evaluationHeightInM)
        {
            // actual data limit of the paper is 109 cm but, since regression is well behaved, allow use with somewhat larger stems
            if ((dbhInCm < 0.0F) || (dbhInCm > 120.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(dbhInCm), "Diameter of " + dbhInCm.ToString(Constant.Default.DiameterInCmFormat) + " cm is either negative or exceeds the regression limit of Maguire and Hann 1990.");
            }
            if ((heightInM < 0.0F) || (heightInM > 75.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(heightInM), "Height of " + heightInM.ToString(Constant.Default.HeightInMFormat) + " m is either negative or exceeds regression limit of 75.0 m.");
            }
            // fitting limit of the paper is 30 cm but, based on discussion with Maguire and greater stability of the Walters and Hann regression form
            // used compared to the Kozak 2004 form, allow lower heights
            if ((evaluationHeightInM < 0.1F * Constant.MetersPerFoot) || (evaluationHeightInM > Constant.DbhHeightInM))
            {
                throw new ArgumentOutOfRangeException(nameof(evaluationHeightInM), "Evaluation height of " + evaluationHeightInM.ToString(Constant.Default.HeightInMFormat) + " m is less than the regression limit of 0.25 feet or exceeds breast height (" + Constant.DbhHeightInM + ").");
            }

            // for now, no effort is made to resolve discontinuities between Curtis and Arney 1977 and Maguire and Hann 1990
            float dbhInInches = Constant.InchesPerCentimeter * dbhInCm;
            float evaluationHeightInFeet = Constant.FeetPerMeter * evaluationHeightInM;

            // Maguire and Hann 1990, equations 2 and 3, fitted for heights of 1.0-4.5 feet
            float diameterOutsideBarkAt1Foot = 1.10767F * MathF.Exp(0.0710044F * (heightInM - heightToCrownBaseInM) / heightInM) * dbhInInches;
            float outsideRatioDbhTo1Foot = dbhInInches / diameterOutsideBarkAt1Foot;
            float outsideRatio = MathF.Pow(1.0F / 3.5F * (4.5F - MathF.Pow(outsideRatioDbhTo1Foot, 2.0F / 3.0F) - evaluationHeightInFeet * (1.0F - MathF.Pow(outsideRatioDbhTo1Foot, 2.0F / 3.0F))), 1.5F);
            float diameterOutsideBark = Constant.CentimetersPerInch * (outsideRatio * diameterOutsideBarkAt1Foot);

            // alternate implementation from Curtis and Arney 1977
            // Curtis RO, Arney JD. 1977. Estimating D.B.H. from stump diameters in second-growth Douglas-fir. Research Note PNW-297, US
            //   Forest Service. https://www.fs.fed.us/pnw/olympia/silv/publications/opt/167_CurtisArney1977.pdf
            // Curtis and Arney's dataset includes one stem of 28 inches DBH (71 cm), the rest are 24 inches (61 cm) and smaller
            // if ((dbhInCm < 0.0F) || (dbhInCm > 71.0F))
            // {
            //     throw new ArgumentOutOfRangeException(nameof(dbhInCm), "Diameter of " + dbhInCm.ToString(Constant.Default.DiameterInCmFormat) + " cm is either negative or exceeds the regression limit of Curtis and Arney 1977.");
            // }

            // solution of Curtis and Arney 1977, equation 1 (valid for heights of 0.25-2 feet), for stump diameter outside of bark
            // using Cardano's method for the roots of third order polynomials
            // (Curtis and Arney assign polynomial coefficients a-d to increasing powers, a reversal of the common order.)
            // float a = 0.12327F - 0.027392F * evaluationHeightInFeet - dbhInInches; // convert dbh = a + b dob + c dob^2 + d dob^3 to d dob^3 + c dob^2 + b dob + a - dbh = 0 for finding roots
            // float b = 0.64885F + 0.27258F * evaluationHeightInFeet - 0.113191F * evaluationHeightInFeet * evaluationHeightInFeet + 0.025339F * evaluationHeightInFeet * evaluationHeightInFeet * evaluationHeightInFeet - 0.00217612F * evaluationHeightInFeet * evaluationHeightInFeet * evaluationHeightInFeet * evaluationHeightInFeet;
            // float c = 0.0025583F - 0.0011370F * evaluationHeightInFeet + 0.00012634F * evaluationHeightInFeet * evaluationHeightInFeet;
            // float d = -0.000066158F + 0.000014702F * evaluationHeightInFeet;

            // float r = (9.0F * d * c * b - 27.0F * d*d * a - 2.0F * c*c*c) / (54.0F * d*d*d);
            // float q = (3.0F * d * b - c*c) / (9.0F * d*d);
            // Complex s = Complex.Pow(r + Complex.Sqrt(q*q*q + r*r), 1.0F / 3.0F);
            // Complex t = Complex.Pow(r - Complex.Sqrt(q * q * q + r * r), 1.0F / 3.0F);
            // Complex root1 = s + t - c / (3.0F * d);
            // Complex root2 = -0.5 * (s + t) - c / (3.0F * d) + new Complex(0.0, 0.5 * Math.Sqrt(3.0)) * (s - t);
            // Complex root3 = -0.5 * (s + t) - c / (3.0F * d) - new Complex(0.0, 0.5 * Math.Sqrt(3.0)) * (s - t);
            // if (Math.Abs(root3.Imaginary) < 0.000001)
            // {
            //     // At small diameters polynomial roots 1 and 2 are a complex conjugate pair and the third root is real and positive.
            //     diameterOutsideBark = Constant.CentimetersPerInch * (float)root3.Real;
            // }
            // else
            // {
            //     // At diameters above a meter or so, roots 1 and 3 become a complex conjugate pair. Root 2 becomes real but is negative.
            //     throw new ArgumentOutOfRangeException(nameof(dbhInCm), "DBH of " + dbhInCm + " cm is beyond the regression fitting range of Curtis and Arney 1977.");
            // }

            return diameterOutsideBark;
        }

        // Maguire DA, Hann DW. 1990. Bark Thickness and Bark Volume in Southwestern Oregon Douglas-Fir. Western Journal of Applied
        //   Forestry 5(1):5–8. https://doi.org/10.1093/wjaf/5.1.5
        public static float GetDoubleBarkThickness(float dbhInCm, float heightInM, float heightToCrownBaseInM, float evaluationHeightInM)
        {
            if ((dbhInCm < 0.0F) || (dbhInCm > 135.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(dbhInCm), "Diameter of " + dbhInCm.ToString(Constant.Default.DiameterInCmFormat) + " cm is either negative or exceeds regression limit of 135.0 cm.");
            }
            if ((heightInM < 0.0F) || (heightInM > 75.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(heightInM), "Height of " + heightInM.ToString(Constant.Default.HeightInMFormat) + " m is either negative or exceeds regression limit of 75.0 m.");
            }
            if ((evaluationHeightInM < Constant.MetersPerFoot) || (evaluationHeightInM > heightInM))
            {
                throw new ArgumentOutOfRangeException(nameof(evaluationHeightInM), "Evaluation height of " + evaluationHeightInM.ToString(Constant.Default.HeightInMFormat) + " m is less than the regression limit of 1.0 feet or exceeds tree height of " + heightInM.ToString(Constant.Default.HeightInMFormat) + " m.");
            }

            float dbhInInches = Constant.InchesPerCentimeter * dbhInCm;
            //float diameterInsideBarkAtDbhInInches = 0.903563F * MathF.Pow(dbhInInches, 0.98938F); // overpredicts compared to Poudel 2018, by 1.5x at 80 cm DBH (10.3 vs 6.92 cm)
            float diameterInsideBarkAtDbhInInches = Constant.InchesPerCentimeter * PoudelRegressions.GetDouglasFirDiameterInsideBark(dbhInCm, heightInM, Constant.DbhHeightInM);
            Debug.Assert(dbhInInches > diameterInsideBarkAtDbhInInches);
            float doubleBarkThicknessInCm;
            if (evaluationHeightInM >= Constant.DbhHeightInM)
            {
                // above DBH
                // dbti = predicted double bark thickness(in.) at any height, hi
                // DBT = DOB - DIB = predicted double bark thickness(in) at breast height(4.5 ft)
                // DOB = measured breast height dob(in.) DIB = estimated breast height dib (in.) from Equation (A1)
                float X = (Constant.FeetPerMeter * evaluationHeightInM - 4.5F) / (Constant.FeetPerMeter * heightInM - 4.5F);
                float k = 0.3F;
                float I = X <= k ? 0.0F : 1.0F;
                float Z1 = I * ((X - 1) / (k - 1) * (1 + (k - X) / (k - 1)) - 1);
                float Z2 = X + I * ((X - 1) / (k - 1) * (X + k * (k - X) / (k - 1)) - X);
                float Z3 = X * X + I * (k * ((X - 1) / (k - 1)) * (2.0F * X - k + k * (k - X) / (k - 1)) - X * X);
                float barkThicknessRatioAtHeight = 1 + Z1 - 3.856886F * Z2 + 5.634181F * Z3;
                float doubleBarkThicknessAtDbhInInches = dbhInInches - diameterInsideBarkAtDbhInInches;
                doubleBarkThicknessInCm = Constant.CentimetersPerInch * (barkThicknessRatioAtHeight * doubleBarkThicknessAtDbhInInches);
            }
            else
            {
                // DBH to 1 foot
                float evaluationHeightInFeet = Constant.FeetPerMeter * evaluationHeightInM;
                float diameterOutsideBarkAt1foot = 1.10767F * MathF.Exp(0.0710044F * (heightInM - heightToCrownBaseInM) / heightInM) * dbhInInches;
                float outsideRatioDbhTo1Foot = dbhInInches / diameterOutsideBarkAt1foot;
                float outsideRatio = MathF.Pow(1.0F / 3.5F * (4.5F - MathF.Pow(outsideRatioDbhTo1Foot, 2.0F / 3.0F) - evaluationHeightInFeet * (1.0F - MathF.Pow(outsideRatioDbhTo1Foot, 2.0F / 3.0F))), 1.5F);
                float diameterOutsideBark = outsideRatio * diameterOutsideBarkAt1foot;

                float diameterInsideBarkAt1Foot = 0.938343F * MathF.Exp(0.101792F * (heightInM - heightToCrownBaseInM) / heightInM) * dbhInInches;
                float insideRatioDbhTo1Foot = diameterInsideBarkAtDbhInInches / diameterInsideBarkAt1Foot;
                float insideRatio = MathF.Pow(1.0F / 3.5F * (4.5F - MathF.Pow(insideRatioDbhTo1Foot, 2.0F / 3.0F) - evaluationHeightInFeet * (1.0F - MathF.Pow(insideRatioDbhTo1Foot, 2.0F / 3.0F))), 1.5F);
                float diameterInsideBark = insideRatio * diameterInsideBarkAt1Foot;
                // Kozak 2004 form is prone to overpredicting neiloid flare, sometimes dramatically
                // E.g. 1 cm DBH, 2 m height -> 48 cm diameter inside bark at 30 cm height.
                // float dibi = Constant.InchesPerCentimeter * PoudelRegressions.GetDouglasFirDiameterInsideBark(dbhInCm, heightInM, evaluationHeightInM);

                doubleBarkThicknessInCm = Constant.CentimetersPerInch * (diameterOutsideBark - diameterInsideBark);
            }

            Debug.Assert((doubleBarkThicknessInCm >= 0.0F) && ((doubleBarkThicknessInCm < 0.2F * dbhInCm) || (doubleBarkThicknessInCm < 0.5F)));
            return doubleBarkThicknessInCm;
        }

        /// <summary>
        /// Get genetic diameter and height growth modifiers.
        /// </summary>
        /// <param name="standAgeInYears">Tree age in years.</param>
        /// <param name="diameterGeneticGain">Genetic diameter gain factor (GWDG, accepted range 0-20).</param>
        /// <param name="heightGeneticGain">Genetic height gain factor (GWHG, accepted range 0-20).</param>
        /// <param name="diameterModifier">Diameter growth modifier.</param>
        /// <param name="heightModifier">Height growth modifier.</param>
        public static void GetGeneticModifiers(float standAgeInYears, float diameterGeneticGain, float heightGeneticGain, out float diameterModifier, out float heightModifier)
        {
            float XGWHG = diameterGeneticGain;
            if (heightGeneticGain > 20.0F)
            {
                XGWHG = 20.0F;
            }
            if (heightGeneticGain < 0.0F)
            {
                XGWHG = 0.0F;
            }
            float XGWDG = diameterGeneticGain;
            if (diameterGeneticGain > 20.0F)
            {
                XGWDG = 20.0F;
            }
            if (diameterGeneticGain < 0.0F)
            {
                XGWDG = 0.0F;
            }

            // SET THE PARAMETERS FOR THE DIAMETER GROWTH MODIFIER
            float A1 = 0.0101054F; // VALUE FOR TAGE = 5
            float A2 = 0.0031F;    // VALUE FOR TAGE => 10
            float A;
            if (standAgeInYears <= 5.0F)
            {
                A = A1;
            }
            else if ((standAgeInYears > 5.0F) && (standAgeInYears < 10.0F))
            {
                A = A1 - (A1 - A2) * (standAgeInYears - 5.0F) / 5.0F;
            }
            else
            {
                A = A2;
            }

            // SET THE PARAMETERS FOR THE HEIGHT GROWTH MODIFIER
            float B1 = 0.0062770F;                      // VALUE FOR TAGE = 5
            float B2 = 0.0036F;                         // VALUE FOR TAGE => 10
            float B;
            if (standAgeInYears <= 5.0F)
            {
                B = B1;
            }
            else
            {
                if ((standAgeInYears > 5.0F) && (standAgeInYears < 10.0F))
                {
                    B = B1 - (B1 - B2) * (standAgeInYears - 5.0F) / 5.0F;
                }
                else
                {
                    B = B2;
                }
            }

            // GENETIC GAIN DIAMETER GROWTH RATE MODIFIER
            diameterModifier = 1.0F + A * XGWDG;

            // GENETIC GAIN HEIGHT GROWTH RATE MODIFIER
            heightModifier = 1.0F + B * XGWHG;
        }

        internal static float GetNeiloidHeight(float dbhInCm, float heightInM)
        {
            // approximation from plotting families of Poudel et al. 2018 dib curves in R and fitting the neiloid inflection point
            // from linear regressions in PoudelRegressions.R
            float heightDiameterRatio = heightInM / (0.01F * dbhInCm);
            float neiloidHeightInM = -0.7F + 1.0F / (0.02F * heightDiameterRatio) + 0.01F * (0.8F + 0.045F * heightDiameterRatio) * dbhInCm;
            return MathF.Max(neiloidHeightInM, Constant.Bucking.DefaultStumpHeightInM);
        }

        /// <summary>
        /// Estimate growth effective age for Douglas-fir and grand fir using Bruce's (1981) dominant height model.
        /// </summary>
        /// <param name="site">Site growth constants.</param>
        /// <param name="treeHeight">Tree height in feet.</param>
        /// <param name="potentialHeightGrowth"></param>
        /// <returns>Growth effective age in years.</returns>
        /// <remarks>
        /// Bruce D. 1981. Consistent Height-Growth and Growth-Rate Estimates for Remeasured Plots. Forest Science 27(4):711-725. 
        ///   https://doi.org/10.1093/forestscience/27.4.711
        /// </remarks>
        public static float GetPsmeAbgrGrowthEffectiveAge(SiteConstants site, float timeStepInYears, float treeHeight, out float potentialHeightGrowth)
        {
            float XX1 = MathV.Ln(treeHeight / site.SiteIndexFromGround) / site.B1 + site.X2toB2;
            float growthEffectiveAge = 500.0F;
            if (XX1 > 0.0F)
            {
                growthEffectiveAge = MathV.Pow(XX1, 1.0F / site.B2) - site.X1;
            }

            float potentialHeight = site.SiteIndexFromGround * MathV.Exp(site.B1 * (MathV.Pow(growthEffectiveAge + timeStepInYears + site.X1, site.B2) - site.X2toB2));
            potentialHeightGrowth = potentialHeight - treeHeight;

            return growthEffectiveAge;
        }

        public static Vector256<float> GetPsmeAbgrGrowthEffectiveAgeAvx(SiteConstants site, float timeStepInYears, Vector256<float> treeHeight, out Vector256<float> potentialHeightGrowth)
        {
            Vector256<float> B1 = Vector256.Create(site.B1);
            Vector256<float> B2 = Vector256.Create(site.B2);
            Vector256<float> X2toB2 = Vector256.Create(site.X2toB2);
            Vector256<float> siteIndexFromGround256 = Vector256.Create(site.SiteIndexFromGround);
            Vector256<float> X1 = Vector256.Create(site.X1);

            Vector256<float> XX1 = Avx.Add(Avx.Divide(MathAvx.Ln(Avx.Divide(treeHeight, siteIndexFromGround256)), B1), X2toB2);
            Vector256<float> xx1lessThanZero = Avx.CompareLessThanOrEqual(XX1, Vector256<float>.Zero);
            Vector256<float> growthEffectiveAge = Avx.Subtract(MathAvx.Pow(XX1, Avx.Reciprocal(B2)), X1);
            growthEffectiveAge = Avx.BlendVariable(growthEffectiveAge, Vector256.Create(500.0F), xx1lessThanZero);

            Vector256<float> timeStepInYearsPlusX1 = Vector256.Create(timeStepInYears + site.X1);
            Vector256<float> potentialHeightPower = Avx.Multiply(B1, Avx.Subtract(MathAvx.Pow(Avx.Add(growthEffectiveAge, timeStepInYearsPlusX1), B2), X2toB2));
            Vector256<float> potentialHeight = Avx.Multiply(siteIndexFromGround256, MathAvx.Exp(potentialHeightPower));
            potentialHeightGrowth = Avx.Subtract(potentialHeight, treeHeight);

            return growthEffectiveAge;
        }

        public static Vector256<float> GetPsmeAbgrGrowthEffectiveAgeAvx10(SiteConstants site, float timeStepInYears, Vector256<float> treeHeight, out Vector256<float> potentialHeightGrowth)
        {
            Vector256<float> B1 = Vector256.Create(site.B1);
            Vector256<float> B2 = Vector256.Create(site.B2);
            Vector256<float> X2toB2 = Vector256.Create(site.X2toB2);
            Vector256<float> siteIndexFromGround256 = Vector256.Create(site.SiteIndexFromGround);
            Vector256<float> X1 = Vector256.Create(site.X1);

            Vector256<float> XX1 = Avx10v1.Add(Avx10v1.Divide(MathAvx10.Ln(Avx10v1.Divide(treeHeight, siteIndexFromGround256)), B1), X2toB2);
            Vector256<float> xx1lessThanZero = Avx10v1.CompareLessThanOrEqual(XX1, Vector256<float>.Zero);
            Vector256<float> growthEffectiveAge = Avx10v1.Subtract(MathAvx10.Pow(XX1, Avx512.VL.Reciprocal14(B2)), X1);
            growthEffectiveAge = Avx10v1.BlendVariable(growthEffectiveAge, Vector256.Create(500.0F), xx1lessThanZero);

            Vector256<float> timeStepInYearsPlusX1 = Vector256.Create(timeStepInYears + site.X1);
            Vector256<float> potentialHeightPower = Avx10v1.Multiply(B1, Avx10v1.Subtract(MathAvx10.Pow(Avx10v1.Add(growthEffectiveAge, timeStepInYearsPlusX1), B2), X2toB2));
            Vector256<float> potentialHeight = Avx10v1.Multiply(siteIndexFromGround256, MathAvx10.Exp(potentialHeightPower));
            potentialHeightGrowth = Avx10v1.Subtract(potentialHeight, treeHeight);

            return growthEffectiveAge;
        }

        public static Vector512<float> GetPsmeAbgrGrowthEffectiveAgeAvx512(SiteConstants site, float timeStepInYears, Vector512<float> treeHeight, out Vector512<float> potentialHeightGrowth)
        {
            Vector512<float> B1 = Vector512.Create(site.B1);
            Vector512<float> B2 = Vector512.Create(site.B2);
            Vector512<float> X2toB2 = Vector512.Create(site.X2toB2);
            Vector512<float> siteIndexFromGround256 = Vector512.Create(site.SiteIndexFromGround);
            Vector512<float> X1 = Vector512.Create(site.X1);

            Vector512<float> XX1 = Avx512.Add(Avx512.Divide(MathAvx10.Ln(Avx512.Divide(treeHeight, siteIndexFromGround256)), B1), X2toB2);
            Vector512<float> xx1lessThanZero = Avx512.CompareLessThanOrEqual(XX1, Vector512<float>.Zero);
            Vector512<float> growthEffectiveAge = Avx512.Subtract(MathAvx10.Pow(XX1, Avx512.Reciprocal14(B2)), X1);
            growthEffectiveAge = Avx512.BlendVariable(growthEffectiveAge, Vector512.Create(500.0F), xx1lessThanZero);

            Vector512<float> timeStepInYearsPlusX1 = Vector512.Create(timeStepInYears + site.X1);
            Vector512<float> potentialHeightPower = Avx512.Multiply(B1, Avx512.Subtract(MathAvx10.Pow(Avx512.Add(growthEffectiveAge, timeStepInYearsPlusX1), B2), X2toB2));
            Vector512<float> potentialHeight = Avx512.Multiply(siteIndexFromGround256, MathAvx10.Exp(potentialHeightPower));
            potentialHeightGrowth = Avx512.Subtract(potentialHeight, treeHeight);

            return growthEffectiveAge;
        }

        public static Vector128<float> GetPsmeAbgrGrowthEffectiveAgeVex128(SiteConstants site, float timeStepInYears, Vector128<float> treeHeight, out Vector128<float> potentialHeightGrowth)
        {
            Vector128<float> B1 = Vector128.Create(site.B1);
            Vector128<float> B2 = Vector128.Create(site.B2);
            Vector128<float> X2toB2 = Vector128.Create(site.X2toB2);
            Vector128<float> siteIndexFromGround128 = Vector128.Create(site.SiteIndexFromGround);
            Vector128<float> X1 = Vector128.Create(site.X1);

            Vector128<float> XX1 = Avx.Add(Avx.Divide(MathAvx.Ln(Avx.Divide(treeHeight, siteIndexFromGround128)), B1), X2toB2);
            Vector128<float> xx1lessThanZero = Avx.CompareLessThanOrEqual(XX1, Vector128<float>.Zero);
            Vector128<float> growthEffectiveAge = Avx.Subtract(MathAvx.Pow(XX1, Avx.Reciprocal(B2)), X1);
            growthEffectiveAge = Avx.BlendVariable(growthEffectiveAge, Vector128.Create(500.0F), xx1lessThanZero);

            Vector128<float> timeStepInYearsPlusX1 = Vector128.Create(timeStepInYears + site.X1);
            Vector128<float> potentialHeightPower = Avx.Multiply(B1, Avx.Subtract(MathAvx.Pow(Avx.Add(growthEffectiveAge, timeStepInYearsPlusX1), B2), X2toB2));
            Vector128<float> potentialHeight = Avx.Multiply(siteIndexFromGround128, MathAvx.Exp(potentialHeightPower));
            potentialHeightGrowth = Avx.Subtract(potentialHeight, treeHeight);

            return growthEffectiveAge;
        }

        /// <summary>
        /// Calculate Douglas-fir and ponderosa growth effective age and potential height growth for southwest Oregon.
        /// </summary>
        /// <param name="isDouglasFir">Douglas-fir coefficients are used if ISP == 1, ponderosa otherwise.</param>
        /// <param name="SI">Site index (feet) from breast height.</param>
        /// <param name="HT">Height of tree.</param>
        /// <param name="GEAGE">Growth effective age of tree.</param>
        /// <param name="PHTGRO">Potential height growth increment in feet.</param>
        /// <remarks>
        /// Derived from the code in appendix 2 of Hann and Scrivani 1987 (FRL Research Bulletin 59). Growth effective age is introduced in 
        /// Hann and Ritchie 1988 (Height Growth Rate of Douglas-Fir: A Comparison of Model Forms. Forest Science 34(1):165–175).
        /// </remarks>
        public static void GetDouglasFirPonderosaHeightGrowth(bool isDouglasFir, float SI, float HT, out float GEAGE, out float PHTGRO)
        {
            // range of regression validity is undocumented, assume at least Organon 2.2.4 minimum height is required
            // Shorter trees can cause the growth effective age to become imaginary.
            Debug.Assert(HT >= 4.5F);

            // BUGBUG these are a0, a1, and a2 in the paper
            float B0;
            float B1;
            float B2;
            if (isDouglasFir)
            {
                // PSME
                B0 = -6.21693F;
                B1 = 0.281176F;
                B2 = 1.14354F;
            }
            else
            {
                // PIPO
                B0 = -6.54707F;
                B1 = 0.288169F;
                B2 = 1.21297F;
            }

            float BBC = B0 + B1 * MathV.Ln(SI);
            float X50 = 1.0F - MathV.Exp(-1.0F * MathV.Exp(BBC + B2 * 3.912023F));
            float A1A = 1.0F - (HT - 4.5F) * (X50 / SI);
            if (A1A <= 0.0F)
            {
                GEAGE = 500.0F;
                PHTGRO = 0.0F;
            }
            else
            {
                GEAGE = MathF.Pow(-1.0F * MathV.Ln(A1A) / (MathV.Exp(B0) * MathF.Pow(SI, B1)), 1.0F / B2);
                float XAI = 1.0F - MathV.Exp(-1.0F * MathV.Exp(BBC + B2 * MathV.Ln(GEAGE)));
                float XAI5 = 1.0F - MathV.Exp(-1.0F * MathV.Exp(BBC + B2 * MathV.Ln(GEAGE + 5.0F)));
                PHTGRO = 4.5F + (HT - 4.5F) * (XAI5 / XAI) - HT;
            }
        }

        /// <summary>
        /// Get Swiss needle cast diameter and height growth modifiers.
        /// </summary>
        /// <param name="foliageRetentionInYears">Foliage retention? (DOUG?)</param>
        /// <param name="diameterGrowthModifier">Diameter growth modifier.</param>
        /// <param name="heightGrowthModifier">Height growth modifier.</param>
        public static void GetSwissNeedleCastModifiers(float foliageRetentionInYears, out float diameterGrowthModifier, out float heightGrowthModifier)
        {
            float clampedFoliageRetention = foliageRetentionInYears;
            if (foliageRetentionInYears > 4.0F) // probably needs to be updated to clamp at three years instead of four
            {
                clampedFoliageRetention = 4.0F;
            }
            if (foliageRetentionInYears < 0.85F)
            {
                clampedFoliageRetention = 0.85F;
            }

            // SET THE PARAMETERS FOR THE DIAMETER GROWTH MODIFIER
            float A1 = -0.5951664F;
            float A2 = 1.7121299F;
            // SET THE PARAMETERS FOR THE HEIGHT GROWTH MODIFIER
            float B1 = -1.0021090F;
            float B2 = 1.2801740F;

            // SWISS NEEDLE CAST DIAMETER GROWTH RATE MODIFIER
            diameterGrowthModifier = 1.0F - MathV.Exp(A1 * MathF.Pow(clampedFoliageRetention, A2));
            // SWISS NEEDLE CAST HEIGHT GROWTH RATE MODIFIER
            heightGrowthModifier = 1.0F - MathV.Exp(B1 * MathF.Pow(clampedFoliageRetention, B2));
        }

        // Collection of pre-calculable site constants used by Bruce 1981's height growth increments.
        public class SiteConstants
        {
            public float B1 { get; private init; }
            public float B2 { get; private init; }
            public float SiteIndexFromGround { get; private init; }
            public float X1 { get; private init; }
            public float X2toB2 { get; private init; }
            public float X2 { get; private init; }
            public float X3 { get; private init; }

            public SiteConstants(float siteIndexFromGroundInFeet)
            {
                if ((siteIndexFromGroundInFeet < Constant.Minimum.SiteIndexInFeet) || (siteIndexFromGroundInFeet > Constant.Maximum.SiteIndexInFeet))
                {
                    throw new ArgumentOutOfRangeException(nameof(siteIndexFromGroundInFeet));
                }

                this.X3 = siteIndexFromGroundInFeet / 100.0F;
                this.X2 = 63.25F - siteIndexFromGroundInFeet / 20.0F;
                this.X1 = 13.25F - siteIndexFromGroundInFeet / 20.0F;
                this.SiteIndexFromGround = siteIndexFromGroundInFeet;
                this.B2 = -0.447762F - 0.894427F * this.X3 + 0.793548F * this.X3 * this.X3 - 0.171666F * this.X3 * this.X3 * this.X3;
                this.X2toB2 = MathV.Pow(this.X2, this.B2);
                this.B1 = MathV.Ln(4.5F / siteIndexFromGroundInFeet) / (MathV.Pow(X1, B2) - this.X2toB2);
            }

            // if this class becomes mutable, implement copy constructor and call in OrganonStand's copy constructor
            //public SiteConstants(SiteConstants other)
            //{
            //    this.B1 = other.B1;
            //    this.B2 = other.B2;
            //    this.SiteIndexFromGround = other.SiteIndexFromGround;
            //    this.X1 = other.X1;
            //    this.X2toB2 = other.X2toB2;
            //    this.X2 = other.X2;
            //    this.X3 = other.X3;
            //}
        }
    }
}
