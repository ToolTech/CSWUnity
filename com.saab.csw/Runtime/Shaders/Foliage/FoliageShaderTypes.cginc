/*
 * Copyright (C) SAAB AB
 *
 * All rights, including the copyright, to the computer program(s)
 * herein belong to Saab AB. The program(s) may be used and/or
 * copied only with the written permission of Saab AB, or in
 * accordance with the terms and conditions stipulated in the
 * agreement/contract under which the program(s) have been
 * supplied.
 *
 * Information Class:          COMPANY RESTRICTED
 * Defence Secrecy:            UNCLASSIFIED
 * Export Control:             NOT EXPORT CONTROLLED
 */

#ifndef SAAB_FOLIAGE_SHADER_TYPES_INCLUDED
#define SAAB_FOLIAGE_SHADER_TYPES_INCLUDED

#include "FoliageBufferTypes.cginc"

struct FS_INPUT
{
    float4 pos                       : SV_POSITION;

    float3 worldPos                  : TEXCOORD0;
    nointerpolation float3 center    : TEXCOORD1;

    half2 uv                         : TEXCOORD2;
    nointerpolation uint layer       : TEXCOORD3;

    nointerpolation half radius      : TEXCOORD4;

    nointerpolation uint normal      : TEXCOORD5; // normal oct packed
    nointerpolation uint colorA      : COLOR0;    // RGBA8 (tint + alpha)
};

#endif
