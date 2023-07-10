// -------------------------------------
// Structs
struct MotionVectorAttributes
{
    float4 position             : POSITION;
    float3 positionOld          : TEXCOORD4;
    
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct MotionVectorVaryings
{
    float4 positionCS           : SV_POSITION;
    float4 positionVP           : TEXCOORD0;
    float4 previousPositionVP   : TEXCOORD1;

    UNITY_VERTEX_INPUT_INSTANCE_ID 
    UNITY_VERTEX_OUTPUT_STEREO
};

float _kMotionPerObjectFac;

// -------------------------------------
// Vertex
MotionVectorVaryings vert(MotionVectorAttributes input)
{
    MotionVectorVaryings output = (MotionVectorVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionCS = TransformObjectToHClip(input.position.xyz);

    // this works around an issue with dynamic batching
    // potentially remove in 5.4 when we use instancing
    #if defined(UNITY_REVERSED_Z)
        output.positionCS.z -= unity_MotionVectorsParams.z * output.positionCS.w;
    #else
        output.positionCS.z += unity_MotionVectorsParams.z * output.positionCS.w;
    #endif
    
    output.positionVP = mul(UNITY_MATRIX_VP, mul(UNITY_MATRIX_M, input.position));
    output.previousPositionVP = mul(_PrevViewProjMatrix, mul(unity_MatrixPreviousM, unity_MotionVectorsParams.x == 1 ? float4(input.positionOld, 1) : input.position));
    return output;
}

// -------------------------------------
// Fragment
half4 frag(MotionVectorVaryings input) : SV_Target
{   
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    // Note: unity_MotionVectorsParams.y is 0 is forceNoMotion is enabled
    bool forceNoMotion = unity_MotionVectorsParams.y == 0.0;
    if (forceNoMotion)
    {
        return float4(0.0, 0.0, 0.0, 0.0);
    }
    
    // Calculate positions
    input.positionVP.xy = input.positionVP.xy / input.positionVP.w;
    input.previousPositionVP.xy = input.previousPositionVP.xy / input.previousPositionVP.w;

    // Calculate velocity
    float2 velocity = (input.positionVP.xy - input.previousPositionVP.xy);
    #if UNITY_UV_STARTS_AT_TOP
        velocity.y = -velocity.y;
    #endif

    // Convert from Clip space (-1..1) to NDC 0..1 space.
    // Note it doesn't mean we don't have negative value, we store negative or positive offset in NDC space.
    // Note: ((positionCS * 0.5 + 0.5) - (previousPositionCS * 0.5 + 0.5)) = (velocity * 0.5)
    return half4(velocity.xy * 0.5, 0, 0) * _kMotionPerObjectFac;
}

half4 frag_zero(MotionVectorVaryings input) : SV_Target
{
    return 0;  
}