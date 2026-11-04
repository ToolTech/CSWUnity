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

#ifndef SAAB_FOLIAGE_BUFFER_TYPES_INCLUDED
#define SAAB_FOLIAGE_BUFFER_TYPES_INCLUDED

struct FoliagePoint
{
    float3 Position;    // 12 bytes
    uint ColorA;        // 4 bytes

    uint up;            // 4 bytes
    uint right;         // 4 bytes

    uint packed0;       // 4 -> 28 (Height16 | Random16)
    uint packed1;       // 4 -> 32 (Visibility16 | unused/pad16)
}; // 32 bytes

struct FoliageShaderData
{
    float2 MaxMin;
    float2 Offset;
    float Weight;
    float CullAreaWidth;
}; // 24 bytes

#endif
