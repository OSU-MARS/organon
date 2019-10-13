﻿using System;

namespace Osu.Cof.Organon
{
    internal class Submax
    {
        /// <summary>
        /// Finds power of SDImax line. Sets configuration.A1 (constant of SDImax line) and A2 (exponent of SDImax line, dimensionless).
        /// </summary>
        /// <param name="TRIAL">Hard coded to false in Execute(). MGEXP is subtracted from tree expansion factors if true, otherwise MGEXP is ignored.</param>
        /// <param name="configuration">Organon configuration settings.</param>
        /// <param name="NTREES"></param>
        /// <param name="TDATAI"></param>
        /// <param name="TDATAR"></param>
        /// <param name="MGEXP"></param>
        public static void SUBMAX(bool TRIAL, OrganonConfiguration configuration, Stand stand, float[,] TDATAR)
        {
            // CALCULATE THE MAXIMUM SIZE-DENISTY LINE
            switch (configuration.Variant)
            {
                case Variant.Swo:
                case Variant.Nwo:
                case Variant.Smc:
                    // REINEKE (1933): 1.605^-1 = 0.623053
                    configuration.A2 = 0.62305F;
                    break;
                case Variant.Rap:
                    // PUETTMANN ET AL. (1993)
                    configuration.A2 = 0.64F;
                    break;
                default:
                    throw new NotSupportedException();
            }

            float KB = 0.005454154F;
            float TEMPA1;
            if (configuration.MSDI_1 > 0.0F)
            {
                TEMPA1 = (float)(Math.Log(10.0) + configuration.A2 * Math.Log(configuration.MSDI_1));
            }
            else
            {
                switch (configuration.Variant)
                {
                    case Variant.Swo:
                        // ORIGINAL SWO-ORGANON - Max.SDI = 530.2
                        TEMPA1 = 6.21113F;
                        break;
                    case Variant.Nwo:
                        // ORIGINAL WWV-ORGANON - Max.SDI = 520.5
                        TEMPA1 = 6.19958F;
                        break;
                    case Variant.Smc:
                        // ORIGINAL WWV-ORGANON
                        TEMPA1 = 6.19958F;
                        break;
                    case Variant.Rap:
                        // PUETTMANN ET AL. (1993)
                        TEMPA1 = 5.96F;
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            // BUGBUG need API with maximum species group ID to safely allocate BAGRP
            float[] BAGRP = new float[18];
            for (int treeIndex = 0; treeIndex < stand.TreeRecordsInUse; ++treeIndex)
            {
                int ISPGRP = stand.Integer[treeIndex, 1];
                float DBH = TDATAR[treeIndex, 0];
                float EX1;
                if (TRIAL)
                {
                    EX1 = TDATAR[treeIndex, 3] - stand.MGExpansionFactor[treeIndex];
                }
                else
                {
                    EX1 = TDATAR[treeIndex, 3];
                }
                BAGRP[ISPGRP] = BAGRP[ISPGRP] + KB * DBH * DBH * EX1;
             }

            float TOTBA = 0.0F;
            for (int I = 0; I < 3; ++I)
            {
                TOTBA += BAGRP[I];
            }

            float PDF;
            float PTF = 0.0F; // BUGBUG not intialized in Fortran code
            if (TOTBA > 0.0F)
            {
                if (configuration.Variant <= Variant.Smc)
                {
                    PDF = BAGRP[0] / TOTBA;
                    PTF = BAGRP[1] / TOTBA;
                }
                else
                {
                    // (DOUG? typo for PTF?)
                    // PRA = BAGRP[0] / TOTBA;
                    PDF = BAGRP[1] / TOTBA;
                }
            }
            else
            {
                if (configuration.Variant <= Variant.Smc)
                {
                    PDF = 0.0F;
                    PTF = 0.0F;
                }
                else
                {
                    // (DOUG? typo for PTF?)
                    // PRA = 0.0F;
                    PDF = 0.0F;
                }
            }

            float A1MOD;
            float OCMOD;
            float PPP;
            float TFMOD;
            switch (configuration.Variant)
            {
                case Variant.Swo:
                    if (configuration.MSDI_2 > 0.0F)
                    {
                        TFMOD = (float)(Math.Log(10.0) + configuration.A2 * Math.Log(configuration.MSDI_2)) / TEMPA1;
                    }
                    else
                    {
                        TFMOD = 1.03481817F;
                    }
                    if (configuration.MSDI_3 > 0.0F)
                    {
                        OCMOD = (float)(Math.Log(10.0) + configuration.A2 * Math.Log(configuration.MSDI_3)) / TEMPA1;
                    }
                    else
                    {
                        OCMOD = 0.9943501F;
                    }
                    if (TOTBA > 0.0F)
                    {
                        PPP = BAGRP[2] / TOTBA;
                    }
                    else
                    {
                        PPP = 0.0F;
                    }

                    if (PDF >= 0.5F)
                    {
                        A1MOD = 1.0F;
                    }
                    else if (PTF >= 0.6666667F)
                    {
                        A1MOD = TFMOD;
                    }
                    else if (PPP >= 0.6666667F)
                    {
                        A1MOD = OCMOD;
                    }
                    else
                    {
                        A1MOD = PDF + TFMOD * PTF + OCMOD * PPP;
                    }
                    break;
                case Variant.Nwo:
                case Variant.Smc:
                    if (configuration.MSDI_2 > 0.0F)
                    {
                        TFMOD = (float)(Math.Log(10.0) + configuration.A2 * Math.Log(configuration.MSDI_2)) / TEMPA1;
                    }
                    else
                    {
                        TFMOD = 1.03481817F;
                    }
                    if (configuration.MSDI_3 > 0.0F)
                    {
                        OCMOD = (float)(Math.Log(10.0) + configuration.A2 * Math.Log(configuration.MSDI_3)) / TEMPA1;
                    }
                    else
                    {
                        // Based on Johnson's (2000) analysis of Max. SDI for western hemlock
                        OCMOD = 1.014293245F;
                    }

                    float PWH;
                    if (TOTBA > 0.0F)
                    {
                        PWH = BAGRP[2] / TOTBA;
                    }
                    else
                    {
                        PWH = 0.0F;
                    }
                    if (PDF >= 0.5F)
                    {
                        A1MOD = 1.0F;
                    }
                    else if (PWH >= 0.5F)
                    {
                        A1MOD = OCMOD;
                    }
                    else if (PTF >= 0.6666667)
                    {
                        A1MOD = TFMOD;
                    }
                    else
                    {
                        A1MOD = PDF + OCMOD * PWH + TFMOD * PTF;
                    }
                    break;
                case Variant.Rap:
                    A1MOD = 1.0F;
                    break;
                default:
                    throw new NotSupportedException();
            }
            if (A1MOD <= 0.0F)
            {
                A1MOD = 1.0F;
            }

            configuration.A1 = TEMPA1 * A1MOD;
        }
    }
}
