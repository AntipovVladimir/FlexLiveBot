﻿namespace FlexLiveBot;

public enum LangEnum : int
{
    /* antispam sections: 1000 - 1999 */
    s_antispam_version = 1000,
    s_antispam_saved = 1001,
    s_antispam_dictionary_cleared = 1002,
    s_antispam_exceptions_added = 1003,
    s_antispam_exceptions_unchanged = 1004,
    s_antispam_count = 1005,
    s_antispam_exceptions_put_word = 1006,
    s_antispam_index_not_found = 1007,
    s_antispam_word_adjusted = 1008,
    s_antispam_weight_not_parsed = 1009,
    s_antispam_adjust_weight_requires = 1010,
    s_antispam_dictionary_reloaded = 1011,
    s_antispam_blacklist_loaded = 1012,
    s_antispam_dictionary_loaded = 1013,
    s_antispam_suspects_loaded = 1014,
    s_antispam_exceptions_loaded = 1015,
    s_antispam_detected_emojis = 1016,
    s_antispam_top10_spam = 1017,
    s_antispam_top10_learn = 1018,
    s_antispam_learning_loaded = 1019,
    s_antispam_detected_wrong_unicode = 1020,
    s_antispam_spam_detects = 1021,
    s_antispam_total_score = 1022,
    s_antispam_stat_spamkeywords = 1023,
    s_antispam_stat_blacklist = 1024,
    s_antispam_stat_suspects = 1025,
    s_antispam_search_header = 1026,
    s_antispam_removed_from_dictionary = 1027,
    s_antispam_dictionary_unchanged_notfound = 1028,
    s_antispam_dictionary_put_index_to_remove = 1029,
    s_antispam_dictionary_expanded = 1030,
    s_antispam_dictionary_unchanged = 1031,
    s_antispam_dictionary_put_word = 1032,
    s_antispam_blacklist_removed = 1033,
    s_antispam_blacklist_unchanged_notfound = 1034,
    s_antispam_blacklist_put_word = 1035,
    s_antispam_blacklist_expanded = 1036,
    s_antispam_blacklist_unchanged = 1037,
    s_antispam_blacklist_put_word_to_add = 1038,
    s_antispam_blacklist_found = 1039,
    s_antispam_exception_found = 1040,
    s_antispam_spam_found = 1041,
    s_antispam_text_scoring = 1042,
    s_antispam_emojis_adjusted = 1043,
    s_antispam_unsupported_unicode = 1044,
    s_antispam_total_score_brief = 1045,
    s_antispam_found_currencys = 1046,
    s_antispam_no_exceptions = 1047,
    s_antispam_restrict_blacklisted = 1048,
    s_antispam_banwords_removed = 1049,
    s_antispam_banwords_unchanged_notfound = 1050,
    s_antispam_banwords_put_word = 1051,
    s_antispam_banwords_expanded = 1052,
    s_antispam_banwords_unchanged = 1053,
    s_antispam_banwords_put_word_to_add = 1054,
    s_antispam_banwords_found = 1055,
    s_antispam_help = 1999,

    /* antibw sections: 2000 - 2999 */
    s_antibw_version = 2000,
    s_antibw_saved = 2001,
    s_antibw_all_saved = 2002,
    s_antibw_dictionary_loaded = 2003,
    s_antibw_stat = 2004,
    s_antibw_dictionary_removed = 2005,
    s_antibw_dictionary_unchanged_notfound = 2006,
    s_antibw_dictionary_put_word_to_delete = 2007,
    s_antibw_dictionary_expanded = 2008,
    s_antibw_dictionary_unchanged = 2009,
    s_antibw_dictionary_put_word = 2010,
    s_antibw_help = 2999,

    /* settings sections: 3000 - 3999 */
    s_s_reactonfriends = 3001,
    s_s_countcforward = 3002,
    s_s_countoforward = 3003,
    s_s_reportcopy = 3004,
    s_s_reportenabled = 3005,
    s_s_premiumaffix = 3006,
    s_s_solidarity = 3007,
    s_s_silentsolidarity = 3008,
    s_s_reportchatid = 3009,
    s_s_as_daystounban = 3010,
    s_s_spamscoreforward = 3011,
    s_s_newjoinsilenttime = 3012,
    s_s_maxreactions = 3013,
    s_s_reactionsinterval = 3014,
    s_s_reactions = 3015,
    s_s_spamscorevalue = 3016,
    s_s_set_bw_cleanup= 3017,
    s_s_bw_cleanup = 3018,
    s_s_value_not_parsed = 3019,
    s_s_set_bool_value = 3020,
    s_s_bw_warns_enabled = 3021,
    s_s_set_int_value_1000 = 3022,
    s_s_bw_warn_times = 3023,
    s_s_set_minutes15 = 3024,
    s_s_bw_ban_time = 3025,
    s_s_first_addme_as_admin =3026,
    s_s_close = 3027,
    s_s_select_chat = 3028,
    s_s_u_have_no_admin_rights_in_chat =3029,
    s_s_restrict_new_join_media = 3030,
    s_s_as_cleanup = 3031,
    s_s_back = 3032,
    s_s_select_settings = 3033,
    s_s_select_as_settings = 3034,
    s_s_as_status= 3035,
    s_s_as_solidarity_status = 3036,
    s_s_as_solidarity_silence = 3037,
    s_s_as_premium_affix = 3038,
    s_s_as_report_status = 3039,
    s_s_as_report_copy =3040,
    s_s_as_own_forward= 3041,
    s_s_as_current_forward=3042,
    s_s_as_reactions_status = 3043,
    s_s_as_react_on_friends = 3044,
    s_s_as_days_to_unban = 3045,
    s_s_as_spamscore = 3046,
    s_s_as_spamscore_forward= 3047,
    s_s_as_into_pm = 3048,
    s_s_as_report_target =3049,
    s_s_as_new_join_silence_time= 3050,
    s_s_as_restrict_new_join_media = 3051,
    s_s_as_max_reactions = 3052,
    s_s_as_reactions_interval = 3053,
    s_s_as_cleanup_interval = 3054,
    s_s_select_bw_settings = 3055,
    s_s_bw_enabled = 3056,
    s_s_bw_warns_status = 3057,
    s_s_bw_warns_times = 3058,
    s_s_bw_ban_time_status = 3059,
    s_s_bw_cleanup_interval = 3060,
    s_s_bw_bantime_setup = 3061,
    s_s_not_serving_this_chat = 3062,
    s_s_bw_warns_setup = 3063,
    s_s_bw_cleanup_setup = 3064,
    s_s_as_days_to_unban_setup = 3065,
    s_s_as_njst_setup = 3066,
    s_s_as_spamscorefwd_setup = 3067,
    s_s_as_spamscore_setup = 3068,
    s_s_as_maxreactions_setup = 3069,
    s_s_as_reactions_interval_setup = 3070,
    s_s_chat_not_selected = 3071,
    s_s_in_own_pm = 3072,
    s_s_in_other_pm = 3073,
    s_s_as_report_target_setup = 3074,
    s_s_in_my_pm = 3075,
    s_s_turn_off = 3076,
    s_s_lang_settings = 3077,
    s_s_never = 3078,
    s_s_do_not_ban = 3079,
    s_s_antispam = 3080,
    s_s_antibw = 3081,
    s_s_as_restrict_blacklisted = 3082,
    s_s_as_clean_service_messages = 3083,
    s_s_as_skip_media = 3084,
    s_s_clean_service_messages = 3085,
    s_s_skip_media = 3086,
    
    /* userscore sections: 4000 - 4999 */
    s_uscore_banlist_saved = 4001,
    s_uscore_owner_trust = 4002,
    s_uscore_admin_trust = 4003,
    s_uscore_trust_level = 4004,
    s_uscore_metrics_saved = 4005,
    s_uscore_banlist_loaded = 4006,

    /* channelscore sections: 5000 - 5999 */
    s_cscore_channel_stat = 5001,
    s_cscore_total_messages = 5002,
    s_cscore_total_reactions = 5003,
    s_cscore_total_spams = 5004,
    s_cscore_summary = 5006,
}