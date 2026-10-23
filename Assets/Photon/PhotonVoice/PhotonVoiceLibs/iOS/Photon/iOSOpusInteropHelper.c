int egpv_opus_encoder_ctl(void *st, int request, ...);
int egpv_opus_decoder_ctl(void *st, int request, ...);

int egpv_opus_encoder_ctl_set_ext(void *st, int request, int value) {
    return egpv_opus_encoder_ctl(st, request, value);
}
int egpv_opus_encoder_ctl_get_ext(void *st, int request, int* value) {
    return egpv_opus_encoder_ctl(st, request, value);
}
int egpv_opus_decoder_ctl_set_ext(void *st, int request, int value) {
    return egpv_opus_decoder_ctl(st, request, value);
}
int egpv_opus_decoder_ctl_get_ext(void *st, int request, int* value) {
    return egpv_opus_decoder_ctl(st, request, value);
}
