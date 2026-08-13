using System.Collections.Generic;

namespace Singularity.Core
{
    /// <summary>
    /// THE CATALOGUE, BAKED.
    ///
    /// Minting a cube costs a couple of hundred milliseconds, so asking "does
    /// this already exist" against the ranked game would be minutes of work on a
    /// desktop and the better part of an hour on a phone. The identities are
    /// computed once and pasted in as eight-character ids, in level order, so a
    /// hit can name the cube it collided with.
    ///
    /// It is worth being exact about what this therefore covers: the ranked
    /// catalogue, the ten authored cubes, and this player's own creations. It
    /// cannot know what somebody else built on their own device. Cross-player
    /// uniqueness is not a harder version of this problem, it is a different one,
    /// and it needs a backend to answer.
    /// </summary>
    public static class MintedIds
    {
        public const int Width = 8;

        static readonly string[] Parts =
        {
            "DXotfZaXxLPgZQpslKnlFiHVxKBDhJiWwIGRExADEOCQ526e-rCPxlohAwZ9EiT5EMnzdJV_sdNz9vhSbrmN8qb8juS8SicZ",
            "X4EL-yuFlYZMf7LrnvtffdG0EvNFVRDe-8XSMeK9K0qqXTexbXJ6AF12ekDErnwFWvgBVWTornG16Gg7A7dkuMP1hyq-4aEE",
            "mj_L66uKIFL22GPXsVFiAHWqnTGIayPw4_esyQRqPHBerKx4D1eez7QI2sXIQAH4TAGUFjOZ1fhis8s_ZxsV01oCcXQ6e1QL",
            "h0feIoeeNvPUk9EtRo0EawJx2V5bGoV7-0Y1i5rcRZWaCE5N8PGD-7vFbtTfh7nG4pnjZKzRuLVQ9NaJcLC4ghhu56bZRLI6",
            "cb9SjBxw_TWlNIDEv66UDGKjznP_9epwCSrjxRH0Zf_visbQo5qTMPuex0PuYSa9RuV-wnBRiEUHeMyRN-9FEkgLKaOAkFZE",
            "b_BI1pK51qH516SysOq75690tHNNudOHzcaTD30teESpYzqDV_Bc5FkYoGZwMpv3uaxTQ4od0P2Kd876w7QOtCz9gkV3L-AD",
            "ZszzHakjsfUM_8_-DYfIOWeTYufEmVJ9Ks884inFFfUtwZ09WeT_6f3DL6QBo5FemuV5zZO6-CU_r59qj816pMDKDlCZJrrs",
            "HGJt48Qb9YUvrFA_LNwgL4thEWokcZqcbv8fcfaPYyO8qG62F2b--zh6ul0xZgPy0TP7mZX69pnBQsrbjzddFo0mEBvRaDDM",
            "FBi3xFFFWWRGjEIP8Egbecgr9Fr3nMCSTGB6rvFF6MdLQjvRnfTVBVoUhZx2F8Gisjvt6Ln2yJSj4TcxV6C-eHt09E5FP2Nx",
            "5V6OgUGXwMEkvqGDCf5YFDPTaFb8wZqpFaCcWnrhU7l1bZE-NABOSZigcQ31DXt9CZIFTzmcTtIm5ZSQFDI4ecEJctOKHekN",
            "lI6C2MGYUWmQWFvhGm71kfbibXV4FMWc5HwWacaUU-KZvVLAbcEp-t4YRyzHo9gzPGN6EzPOeGrIRMulS3o4ihiOuB1WO75T",
            "EQSrhmxH293JMy8e8rSK4gT67cxmBvO3M8EoEVeufgeTNIpUlxFEywbfGq5hcWQ7QFoD5emthxVbtt4Hcmw3ReSIDxLelRep",
            "GnaipWUzq3mwr54avXtxhR4OL99DA2XWZJv-fWLLG1UuG6xAPsupahANJV8nxLY1600RFBXdG274FD77uTVFqjc2gqd0IkI3",
            "8P9fn4yP5nRmakmusbf9ZfHv_ms72auOL8Ui-J2llgQ4SB2XPHbmW_EaZYJkzqi0kc9KWpQP_ucqdaRTqiPvfPchBc1i_mFI",
            "T5kkyfp7Ivxi2fKzi3HLt5-38-H1MtQjlfgFGl0rXyzpaNOZ5GyCVQqXUvwEEBgdEqeCYuo9JIg650nGueUUHlc7FFIL-DoN",
            "qdCLig8rAZx4xFerVn7c-sUJgqaPr4aq0_AMKcNn7vCjCKx63IFWEbVcDsxFGe8mqabhf4QNgitmNNW5HzGOSwjYAsMNtexa",
            "BaFSY8zQxjjClaZXxqOv2xwdjPKqB5SlZ-9VsyFCDGKLmaDZMA1i-NDWKhYDpPI3f9ztDoLI8WwAFyKY4ceXESZ_XTYzwpD7",
            "JSN7rJStvg0LIQiRGSLgrAqE-kLbxK9f6q0NbAzD4C9O2vg3W2NKgRSpzKYCYVKVphXJMA5Q-V9510tIWSz41-LBJbNNwuD6",
            "2_w-yCrXZUU2cpfkgViTiT8fjIo_63EnCuwTn9AOfl14Ok2finx6Gw1m7H9xeTRKbP9pmUlFbUWeCDT0_iOols3ClSGxycd-",
            "U8WSegXrj9PYeaLHO5o96K_7iBJrPiLgNkw96Y90SniEfWvd7XBYLScvobEt1RRV81IpSgRn17ZTCNeOu6464qXOUbz-GTZy",
            "acwAzR4BgBB_Dq1BvpEQjQnMLSXY8IHLR5hJTDLrDbKfTHNpnqvLOX2zbMZfDowJHq9EKWsAvlnNTKFOPM6ukYhWGvhqIV-6",
            "mx1OXkRW9YM_HUlsTX6HhdYMWc6gbs4IAO30ttm-XH1VfrHubWUTnzfhyRAP72uu7hjju9dpv5GiadAXVmPHvdJOVRjIcCMA",
            "ja_JH-us9uGsGybap5rNT576HKyOmPkpUr1mi8YTxvUWutmhJj4u95Wa2bYcz8lq43E4ejjeZ6s99JkMN4BHXzTaxvHV8Dhx",
            "m2vpRaS9AjAmJ17Ea08UcqulVcb4qf63ypjQdG5-VtlsGKq-3Vzmihwc7BT1X-q49NB-18wC2Hlk4Ty3VdA9h8NztWewP1ra",
            "I1f1rhatY1kUYXoPU93DGjwbZWwIjCEXX-ewp3R5syTJJbzZK6nrUo7mu__PmvamijEx3KrEcFeE77cjXVs3fF8HRWKrMAKR",
            "tH5cRLJNPkH9m1PYzQYrj1QodXclhrOZGZvBLLMhZmUmDvWXdtkwtZyIcG14Px5T46iR_s4Lt-APxtZQ8pq_SnoZltIWwayL",
            "vRtbJ2hxs-jBP0zhEgzgMUkI81xJX0e3XJzV1krJDbttZ8lN-OCWYA1f1UrSLJQEmPVhyxLRbs88J2yJ0oy5OrQzm74Ol5jb",
            "orhAi1CUcv9S8F6d2CjgnGND7ZXW0khUAn9AqwUicEagmLlN7BkTONlmdqP0MQD1rL6950O9Y4a1gIxJf_2m5_AcKJ7oQfg4",
            "UnYy9vNNAWp7YFtVqPwKb1J1SGCtDzzLDiY7TorCSul4HFfuh3LmFfa58vpsmTq54vp956AASkU_F1aysCCL0xWf-ou74Hlf",
            "gZ9c4nf7cNzl0xqWZD2Rsefu2zela7DuW6UtORHJy5Eh3b7DtW_aJJTan1InWICgvCCINXCo_k7kDE1iK_2Shkjqoi9ZOC6n",
            "XN_Ucz14R8PBzN7fI4_-ciKLT8BYtdVzz5A9Tn_yhfSl-8wW-qqjusf0AYhMqbbmSmb4edBHzK1ZrVgZH90yMQ2E5Fe2OOcK",
            "S3CwZoa7mzzz7YwO32HcXSzhONOlgSe0PI8NRKi3ESKmHl-CMokP1IDqDmB5wSqRZh0spPhaqLWwy_Pfx8M-_Wn9aFbx8wgL",
            "Px8oJ-LMRhu8iLyQ0nF3kIC4HQKPDJ7rzEPv9NLqwYSBh9bF4HtG-glqO_TCwwZnyfl7K3bwwaXoxIt7jzkQK0UU-j8QEDh-",
            "6thpqi0wDwuvVCwX4tE-ovjfDn_GxdTfYRzyAIAz6xue-LjS6WKvAksqmur9mIBZ-6PedAJHuctFdEyZAJbJmAWzeZAtH9y9",
            "rztXpd--hMhWeE8-eRrFchfz3CQmdpnUA72_JfQ2W-X6l8l33F0NIVIIIyXBFt9TgiFfZ3GBjNjkqpLcCJsGovcgXGpO4zDT",
            "ArGTNGyngLJIQVa1VHofTjDQoBQEIXFk87pGpqvkmZ16VvaadYNMRzJJvSdvq6lo78ha-H1vhhsGxaAmNjy4zIxUBrMjapV3",
            "tAc44CbFTY4MCB0XXJ66i54Ga4dxgFiPqKVhsSw0u1HKjla0x8Crz1SSipp2Tk-IjSwPyjfrsQTvvtGb_L8_ENUrJcW4gKio",
            "9CGjvkMtLw8mNw0SCYL-dQ9BUHwI-Y0pt33CbWZYKu_VXneCeZnGFDwsUVcldu71Cgwxs1sLZEVXFRYyb_0trms0D3YTubfd",
            "XF1iXmR6vMJGSORbbecfn-_gnLbxhLrb7iONFT9FKz9Lz2jtjlrJmIo3Mf1EVEk30RWXaIlDWEROSD0IYZir-9hkuDaxLaqq",
            "GkPXDQk5L5QTdwWt4qKbrTu8getv8wlJFnskkapHDhrsb7pvD-lrTHbJgB7JOkdxOCNPikEuXVuPMJ04JnFRM0q_O4OZlU4U",
            "c_B0HvguxAwd6ISuog3XDDRpfbda9AYxgZmquD8VpRukx7cjLBu4FU-UcNMGy3Al2Men2yz_eZbTODfWj04Osz-aiWnCukj5",
            "LgMzzR3Kjgvkq9F-QnF3YkLXSyyV8RZtuAGsH_6eJsCba4nH3YIe-0znrEAGbL8ah29w2f3m53q-ZouTQL71yazDN7XmXe3S",
            "lGlqRLI1YgRsKcgQKHttzer4nSM5E_I_r6xWebw7hiD2BN3lpV7ePguCAZ4z7yKtztcwMx4ypO-U7vHlnCDDCRWC4NQaFPN0",
            "E-uwFtdU9oVVU9sP_uCCfjjoCW1ZLRizWtLQodMG65LLhqDUjBXVwsJWyCOtycZiVHa1p5u6J-3k83Y3HLUTt5aijnkZ9Cpw",
            "-QcebKKkketlm8T7GPSwe0I--LOBJiqpSSFwO3NIgpz8NJwqYQNeTHUF47AgSxOrpBjk0UOKMBbKmuRKG6UKS2JPQmBxHLJg",
            "2f7UCYDKQq7wxWRzlmSO2YSv_F41co48k9eMhjy9unYaZD96ATmbsB6AQrXkrCbnw2TLmP4wvLECA7PLHKZ1AEsLYBP4m_IH",
            "knNkIWau3Q5KRPlX29A3naNntfJtqiQL4NQIZuty3D5xwUjRIos3_-4XBop-RO-XzVkuRchuFdz0Z9ejooyLuV55l5zxrn6D",
            "Hcvf2qoV68dGv6WaltHTccdalDiP8tTW74rTttwEBteGAPedWsouK6_ClnmPYcm-rcGQ8ohoycuA_fk88eZw71kfzF4b8uCI",
            "gnigS8k9S6EhhL1-fjIsEpvSk7Zu0GKwT1PbqUWkshOd5aao-GNiRPtHhEeS0TIhaJSMRElL2w874h16kBirvaHZOlIu53pm",
            "BgzSgdl3ErgleZxLHRcjNrsqzvj-nFmNMsnDHnQZWAhcVMC2E20bqAxXYe14PV3ZojY95LV2MrrguhSqg97rmBNPl6q0FCBU",
            "lzKmM71aZbnjPGGbGUdQldul5nTNFBHHgTPQoUUN02gy6jVlie_IavNrWfwk29zXPpUz8lZO3L0A9PjtF-VuGoNaFfGPMa-O",
            "KsULtPhLCVlRvwbuseLBGMDKIxDVggS7f4FLuVbjTKl0_OKHWiHzj5hCVciHpRTd9ldl3CFDmwWUVC9c0MmfwnKO0jkagjt4",
            "Q0VViITIMFoQzxGIi50QZ1ufJ3TVc7o3tx8gEia5uocJ2Gtz61NmDl1NFd2iCaKuceOGfUgm7LZRJ42iCQF0DpZcosV7BKqM",
            "sqTm4d2cSsNEmL5IsSUBtN1oisyHY5zsrvMpcp4bz-_y_ZYS048V-b7TSATwkqgCWNcueplXAms9TUbDCbQI3myV2q1lqsms",
            "8S8cJojjshoJ-SmhmdvGemA_fPLC7_XkLZCZov33ujdRNLbIramjUFvI5I37ww1xLONigacuQoz1oRM28ltYhvqjzR4FVZtV",
            "SNx1gEc-WUPVLjHrMpYye0Wr6SEB_NWI456zQFl-E0PWtYrgxP60f_L_3a7RUMDYSB_6eadukphIqi6AYUCdQQ3s2k0fIpjy",
            "tQBA9qiEGS4ECIBaSWEF819ep5srWsp1suQywSqsQ6ecX6sHWd4RxfmQEwuK1qnis4oOSLNowSdscRV8cGic8prYH30q0SNb",
            "f7srnTpRUxVt2Db1D3QWEEAav6n_SqqD369oVqlqVCkuMD3QEBR7vsGKzE03W7kI4Lct3piydG8XcsRhGNst5USm6cbOdfee",
            "QgkLg3vcCUowUnNlwJkSCCROywakSC58umOyAr32l6J0I97QIAFJLkTG-UkNIOMNQ6ECoYpMvdNKW2WVAFbwB1-JPFv2fEvE",
            "-vC00KelmY-cLTt9WW8j60d5tJ4-5d477Mtoe7_QM6kUuCHKc6K0GiYYjq8Qj8Kg9x7mkZvHAowaI9S-7JM6J4RPgQIA0L1L",
            "PIXr_tkdyh3Acq1KJKXKowMQAO3wKAYqdl7efnt-LFOutFh-pGOF6BlaQ_s4r1JXKsemHL8Fjqc9Q8-DB6yoJ0kUsbTqI0D2",
            "H0dZUmm-7_z3YMRfo_k0VypzRpt3Gcs2C5ab-4snnsWDi0Q5m78C1S0gsp6B4ZzIbDMMWVa3zG0GPuqLq09hImqOw9v2dwzR",
            "282MI3J6HuRr7w7YW_tfTV3M1uOxzNUUf9NRcvnUyO-JwB52-172-Fh5qpphBaPTUdF3aC3BDQf-fXvJW_MXEhgoeRdPXTG5",
            "6sKJExPh37DsElsbV3y1e4gIp2C0GCCuUrjB_PRatZmjN_9XfXiEr_9ReJbTapuSlxKXzmfeHXQOarOS7a4zqvZEkRdF0Qbo",
            "15bjxqsAVWfvLdngXfTOts8rFzdkIR3Gnt_Njl6LA4KUL_zCgmYRiKoh6iSqSMVKY7XBZkjogRBrfrl7g67En5e38bCY5ll1",
            "WomzQuloOgua_R57ZPE5kfqc0l15UEpR62ap61VbSeXkqu9dI0YncVXOP31qhNXvRUlizqLSFxB9WfxDq1Okg9qDCPdqJLT_",
            "p64OxTUac8qx-z2eLfoWAiBpaS7pjjnefZ6ibkJ5bUavkd-fukFG9cSJLQVRwP0bKcTiO1xWAP97RkRjPqpubwJP5VyXNbhi",
            "uzHIadpboBU6SpeRZ_x3lQd3Fl6Nwlj7nZDTK3FjVcQV7Xr8NDvhh25UfWf-TLLiHYB6JCACS4ty2HRW426kaSv-5y9n-UYQ",
            "Onqwvesj2nIcBEydjhctstbE7Hjoav9LLLT7JqYm3p_9uxgq5dytq_RdsBVaYKSmU0TOitFF08oBqMoFXHmtAvPOMB1Q3bCV",
            "TS6ctspsfGUFjYs6ZAGXL7Svl6tA_bpg3U2iazQmiUAJepL0NsM69zFe6g50_de-8RAQs4WNgj-zEOaOKVwxLHFB8DFDHGQz",
            "WQjSyHRCywwXMVCAzCnodJBFBxXtz5rhMZiTbS1MnwY_Fq9i4J-k96TdVB9oYahgOwRNRDoeHRxcvyik8BDvIxBy1eQw1VHO",
            "n6Ulr48wfcSnyBEfHhLOU6RrUwA1AdKEYXIRctr61yAD4WjHBwVaoobs7uuSpBlRrRCyCXLU-omK1dafhydzIIIKrrnnXaeE",
            "XUGrGIjnYWrs75CA7S09NNWbhE_q0cEwKumJIAWnXaYkqBOHOQ4vK4XFLQkz_ki-vFARMT07I0G_w91CAQvboG1ktftTSmq0",
            "-b26J_ILcUBuNUoi-qVaWaRa3L4otoiIu4GHWib70DlJ77QN4cyfDoEfWkkR5fP6jnDwX7O6zBXULjvuZPadpH1qJacp4GBy",
            "8iaAA2zRz1hAQBgPiRlgrtJAiX-Um4yOfdD2UhOZSPjL_gAs2KUH_YbMU5Whlxa7CplnVsfuyAq56jokYX2bBxOvWF9OUMec",
            "GcjcCFDNBs_yi0zvjDDQEwPYV3xq-osfZNX4g-ywxGP7braAkaawybPjM6W40-qn6j2MLZZsdTe4rEfjd34ZGfvXJ_82FTDA",
            "OzVwXmEI617vbMSniADEz97hTrsnZ7oH4fIEkB5cTIzMR74jOpMYE3Oftq7Z-3vzvLKMOMjsvICQt4QlfZZluN4G6NESx9D8",
            "1J_aA2hu7XPY_3tkaXo9bnbpEFzIyEthiJQxZLTq7U6tJL-nsYbFCGrO5-tbh1OrpeWIen5ieY7kMKY2w4JIdDXVQ5nEOx5z",
            "pDZlhnDgQh5Jv92QaYDhLKTI86A5cczyN1PQSrQ04UCSAXOFT4UaVRtWWZjOI-zZ5_jML0TnAQpXIv39tCiuDWOwshuA0o5R",
            "zWyNJS0QL3rmHrwyMxaxVDzM6H-Q1OuYdVYVrLJnX2HqnsSdb15J2SKATzpKk5oZufHSYpBy5IdHGduvtHdeHkocJ3h1gEa2",
            "5MgncfOwESo6vb2GuvVtw8WKqu8dCMx20tqRYsAG_fNlMCBC88jChIgFjZLTHvwoCI5oCrsrqGqswyvp0JvdT5-HOiatwygj",
            "RJ7SycfKu94Q3anlMAXiv5oBxhK490_9Gh9sbZ2_4lokhpPlNFxc_OC6wBuc97b-E4zgax1jPhlhVf6vohq4b9PGnAVgiq5W",
            "j2f1ydjuN2yWk_i2hU98zmbizHqZ9-nhzj8qBDjCzZybpQatP2lo73sqUrX2GFRV-xYaCYbn_0c0gBWnFvx1PyzTfCXcnqqT",
            "hWZBTuEYFVjk4SgdxOGzjGbGfdJcqZ638pG1ydswwzw6gyZ-07zCHQkVWenccApCfy_4xy4xU3POlwkkdGitb0vrFA4Xx9do",
            "tSrX53AJzfqPweEgOxRH4u5N_IckRmc1ZhCKGdqJyts1LGFzpqVKDa34ORbsW4vfXPMyQwJLavyH5YUpWy466-5pKAm3fLNm",
            "0HRrN0ovPkKQsmTTs_qRgTjumqcM9s3dtVRj54Apl40vKAyXu11fCTWIjxqRAYYM3CXf6oSkAy_87vff0b4fSirwcTTserWi",
            "Amcs0PY5lxQyhNDOXoliYnI9daEG6CSoNGyw1MEKXfoD3zAnJqOpx3ptG1nkJdbBfA8FIBA804l3l3LmbujG5yEqRpjrUDqX",
            "8jPKmUm0NlEGKiocFibpq10sBeiXW-y0AfGYksLt0AgMQyh3IXZptYnaSAE9y9Two4ZRqAToNNvopcubXbYIOQhjfqn1RMF_",
            "Yo06-V8fBFG20WVhljtVooFBEnHlj_w4IXa1kUQCoDTV9IE7VD3nhJXIA2c-ngUOh3jK14nR6yuWdnQYQNJwWQTKNBRORP6p",
            "Tm32J6uLdcwfgpa_e1eYBT0aM7zI5pK1C9tOvdCWCyTGXs1ML57oQyANwiGoAg3ynBOKkhXN3zImzK_V08F032Oaqnh-NShB",
            "O8HgLQcDcsbXxDr48dqzZ0belAv8gia0369M06A8e53nTsIEL8oHeCtbFyX0Dw0vPYaySZHYTFB3TpZkbzRRhDPf0qcD7KW6",
            "HcLbC78JpLn9cflKG5G46fRZWA_eLQHzxIL5QdyvOcuiby-ktmv2ukeCr5rSjeMn8qk000E5M9kljnQPzVrslqO-9OVp54QD",
            "H1k9_ch-uLvrCqSph1vEXK7i0Mw3bgzJReTOLmey0cimAICaQ2KJ9Q1SmmFyzFDTVvS2cofosixssK3j2Z3zEAqSqF6kXbqF",
            "Iwi-WGuqJbDT9hZvjOQyoR86-3F5kd2pa8CtOxC1y1fHGgFwzVLGImDWQaWX56kYAg3A92evUdkRpTJ0Um4L68xmomNtZiCa",
            "a1Qwx5i3w6q63Q0axf9fmTkLRga73JJWTkyJmYDGAMS2XHVgHSR-TPoaZF_yOdwaBqH8PUVGX0TGldIQEMSVgFtmrMGtIrhq",
            "Mk4NqDdF2GYuAjQpJ0dXQt6kgozK4gbhcl_9J4nPLPQwgIZiN-5c6q7uCiGYFNq9tIRZJzA5ywKCLpOSgkqqDtzMdKZn96LN",
            "uDSE9R8wykK9nE6B2AGE09vAB7k3pK-bmltKulRrfxDoXYq6OyNnxjvEDqF1WCddEhRIRak1SR-gj_3w16IEyoHuxr6YLYz1",
            "LG8-qY2NpWKQrFgKJlRhoQ535gHwEZYMSHLz6Mt5R8W9UWFQ9OCS_m0Ktf2pyOGWxO-225Tq6IdAOjyCiZz0SLjLss6BVmtJ",
            "GPHxC0pnLIM-gQq-L0pbYyWp8BMmMpn5VB5LmiSn2Sy0NsJznTu3bzPjji_a6T1Ge2N_4jQcn53fm5ToOveM4xSve5x9Jwo3",
            "QoZp6wPpW8S_nVrwzYlJ2NsoMTmV6ZecRfEMuw2WwIze7gHQTlwObaczVc_n12xH2dzWyiw_252_Tu36bcdU0oYPMgi4VIFp",
            "-58zZ_XAJo_P92sXdjHcHE8sLE9-oWnUTQm_XeXfMyPgZXYFFQDBMNVjgTG5hFx_wg9MoFFuMXtGA2yE_FKA0kZ6qlPxOGRM",
            "Q5OnaV1pM2tZyca3x5LVKjob5_hmaT9VsU78lGJyI8BHdKwNkUb39BwJixH6r5b7J5puTYMXv3JGWKnnD7NyyR9FkUayE-pI",
            "uL7tod28nlrdLNsUgMfdxFwPafMVI_QZTZCfou9SMdGG_JF8cIRZnqpkdik3hCIrhl6UGKm6sjcaICfIbF6LSVqcWVYZ1Fef",
            "zrV_hmFcOF-_uiRd1JiIm4F1rMnIKv3qo_NwrMm9a3hQU7pcqS6JRTfu_0tJMeiIQiAa7Ip3vNSO_kKAN3xAc9C0VyRbWyMa",
            "JHuVk-ru0rZTLWG0Fw5XBIBZ-Lb2s0dqYiAVLmNWBBHI3D0FsMOdthOHx9RpAm4Yjlt05nOoP-WLROwaXVG36KGh8NRhvPZI",
            "xtgMqxuqI6lF2tyke3owr-siboPVKW5oEruXgp2iZHJZUFpmim83BI00GAeGTukwTkTSjRrnAJAIa-a0iFS2sOW2QCtLWbIW",
            "ZTIgQqcmwA4YPPq4BKc33bukMaNWKUuvieyqc3wIx3FzqJar3aadywwvNBqIc5VmCOktNqRHo9KwtFzGSPPC6sEBamViJT-3",
            "RV4wtarlV8YwBmafJ7mzQYvqreb3LJVi8ScqlCnwoLwyIG2bfX8CiiCiz0crEguiNGjTsg4oEidasWDK6LDXxzMxNEryOAK_",
            "4dJtCRf0D9kzVXOcuGdSmnXMST7UAWCAS-MFLRRtCj1ARoNI_WXGCLwC5iKTBUyttXXwlcUckf0av0rJdQn_AUnkjNTDUtJu",
            "ehLwYDu0WMYpdu0ZYvHfJTYG6ImOeAZHVoAAsxe4B_si-6cHz5aornrOSCKoVY_955qV6a9qZwudykA6KwKaUlBeglJyToFQ",
            "NsCAvl9sc33pRhFgpE6-WWKOn1jOnhVqhds2gsVHNGSfiblav2IP8Gd-XNDZgrLa2xfDnk9LUM2_b4-jZf_TKCU-oyfsB8ON",
            "SOQxT43G8sYxONQS2zImjMWMwNckMtwfjtbU7Z58lpt15aAV7mZPfh9YQCmQE1b9f4T9A23p8bB_ur1Pcg0TPuGCGILgnGGQ",
            "rlIZar7QngrHkLFAhYgLp0dH4PKo4IDmOMkhyK2HQ-xJuLFzqbWUef7e0G9DbG2ArS0Q5na2l_A6kupuRhSN-tYn_pJXHWZH",
            "3xmtEwgxNofgnuQvqBjhG5XrqbyC9OXgLz3zKEhsYUEK-JrZwLEwYfZs5iNTT4VCH6PRvru_kZWgCONhz_iPqhmL-UB6r3f-",
            "oOchBohtQ2BQp2JAHmshZ3ptKibDERDk7XoO8lG4RtV-Td95JFJs0-yBCGiFYfuadY4xeC3483gAcyT48ygG32x_Et3k7o74",
            "OaCAjGX3Jz55yr76n2MbHFPIgwRZWdLuOX80BHUXVjQshRiN-lSL1ZMxEn8SHbjnZC626yC1xm6hRyCpqVonx_rkjKJ7tPMP",
            "lugkhJ8SoYoBcgHxvulFtaQaLhGNHe_Jd1zuJKMpsppP5joutsxXc3dcCeTW0PecnB7W4MuZWMtjHRydxWuvXWnGPraewN2O",
            "8SdEJGM-7nGk4Rhsb18rU-YTaqhuzP2rS9H1TDo-uoQwT3xwdUrnw9Tb8XGphSRNqHvKmeuBx2w65VzNVgmbIxSIXUl9r7d6",
            "aiK2BFlx9d4FEtEQmilJYnNQdIOLUfaQ2pIemMGVBosJ8XfbKjwhxxukGN6zIX8E1E2vA0-EVSbefNLQwpkHRwVerYOQ-6Ve",
            "YGDBZ0T5h1deUJk1B2KV8LZYlY-SWePv0T8icOJQ70ehWFagbzLabMDW5afD6_Ijc6h-Dgj0HzLhKQZtWDISMN5Mo4RGx5-m",
            "LgC_t5klsqmgwjioluKYQY8f5slV-us68dQNsPX12d0MabZC7eYp481j1zWS4z4UIYFZM_puOfjOs-1TzvHhRywVS9EqBNMX",
            "smU6tUajVIREO4oQ1UtK1Oy3PadMI6T8ncusfDNekgLyGOOpykECTbiVFUFeDRxa2jNN02FGAfWeDJrZ1OKRO95kVtQOczTP",
            "Ygh6bW_po1HXgPbMwJf5zF9KMp8DEzMyW-rK5WeMUXblOBPP38tUCLH82locjtHj8vZe0pMRZsLuhr52QFd3wmhz2XrrXQ3s",
            "iDDIpsjbQcTmF7AJr7-up6u3ORjD6PtD4qBCqB_Rqljdmy1cmEMGPfV4sb6yHBaYXlTW008WS9cXP8Ko_AnjR5bOxwT2o8U8",
            "bkiXizHXFK_iRdU3ssPx3Rm1R9_vlTFtAPrpjSWtLTssLQuZSfNIXwGnaWg1xICCnRNI9Lm1uQbLhbfeYMo9D33UexI86ar2",
            "VOQNm-61uOcnUEaErBXxVsAg456ghbxNzw48sKz6s49YTUNx9nKH89r-HwmB6YJAjVq0sqCN4fRyDGwHFEHXm5GvfGvPbdMX",
            "_2z715eUhuKxsR6ECVnvRCZgJHdSw_Yi5KRc1e8j4dpwrzFXzBMwZbO9pirTjUy0WMFJJnyT4zRPF1SiuZiK8d8NxKJAgH9c",
            "uJ9zpTBEp0q_TdsZ84tDrbeJUKLtA393sKC7XiLp3N15p1HBGcYwIZyR9WzoBnKQ78O7XNYIe-hOnhzC64pvpNUmftgqYpxH",
            "cwQnmwoxsVMJ2pFXbaXAYP88SoloYqn0-bF8HVtJYmc-69CieyYJU3MdfUYw2kfmN7lqPgTV-99_HFvC9mBEegryGPtEsacI",
            "ZNHHExHzJFBanu11CqnvSfl9TlOG8n124HvFDa9dH_tjyBqTQjWpGZNO0OhuJzK9BHXAQcfw-8VvrDAbahZ2zYa1F9K4-K0D",
            "C9xul75EPJCUikwa-LICZY7gpKxWuj9eLK5TgaAP4G0skuTLqydUfVEmGSDWPInDulEky7dIjtfWx8UNmkmmobAOjucSdXYv",
            "h68moI5XU8E_KZvZXXAjzGF3_Gh-8W8m0q1NKmQD-UdupyIK6O3lp-ELehhIRPjg9UsOc_LR5E_j1VUu1-olZ-mKw6LO5923",
            "qqhlWrzBR0WapDlej4JdEHPEYG3E4JiSGe9bwXp3JqB8M3PpOcFKrTejIe0-Hlhmfj01w5GgSq4iR1kcdgqls841fiDUVwei",
            "ZfQoEq_Cql5W-eLUkF1AFtShHpTSvObm9p0qdlNQPnNqPYx5TccKZqD9jX6KWB6r2ODjcmusZGhLQrlaBxdLsluakenifQAv",
            "TXpyRTPhsCiTW3YiaGoZTZEVFjDX-QNXFkoJZR6-5FG5fpE70Iwe2UjxnfzVlIOihdVPvNnK7W42vmQVWUGELqV6CLgXcW_w",
            "ROQ9eOLp_1Ink5zwbkNrw_1wJc9MP48ddMxvX_BFT1jJwfzh8JB5Yu-P3lGVBwVDiF6Nw6c3rl9tXalpYcXp1aOBc0TkiWFD",
            "cxEr3zLgUSluaQjToZzSaafBlMAipgxZTOmHKsNfOu7BQQBIE-DjsNhPeQ3tsf8QgoQ2a5qPnyi2tNAk-g56uX_V4Wd8Q68n",
            "YbghH3O24lyqZP3M_qfQh6qAtxNIWODS3KPs2-ne-79uJ9OZ37ZXtiTXWVacnyhCK6sWJhWSRm-aAA6QsfIfhuNYBppL4Udy",
            "cUrG7q-7ThwugZi_EpByFT5etKxEDsbnakypTRvMXuu14fK8Xr8fxdOgs8mrockaRF97mORRpdX_sjGrYH3it7TPxUs-Be2e",
            "8DVdnkhTB7pA96ezuDr2xQBazevS7axnRd9Y6aG-P863PMFo-OqVHWTBWxP7Tel6pZJVWS1RzRTfu43MMd0jhb37Zpp5kYIM",
            "HRb84MQurjYebJkrui90WaOCsdmVcpeOvRk7KDD5nTP9x_WzK2CaUG6zkjqeanvhWzOibVEc7ZWEQMwzeMQcUelWWxJzxXwh",
            "AsYsdNLV6J1FhJm5A6YRmgVpkwpHr9I7vk3tILCzbe1XSSchY0pe0BRRDN8Hk3GzCRkbICLWX1hGn8dOjoUcPed7KLSjuylj",
            "haJJl2s26Z_uUEfEmMTuPNFhMnNMSZUk6pXaK0enjB161H_D7Qs3CNXkOv3HNc_5fFrYuMsfxn4OkaUMBOhl144VvojDP3aj",
            "F2DzfeqGakjS_sgwtXIYZ51_Jbn-XCMEg6dW6p55f0tHXMfrzlfHJ_lvq8XOCsvP1YY3MYYGtiVtdZcdlywjq_z8V-jyzi86",
            "mXOpT0PoDMyVcbj8Hdrp2KSFR0cvZiWrv-m3LaNJ3m1Zgm96PXXuXtRaygXd7tsJxklyS5kNhgKxy6vB0B_qDyrWYb9sfNqf",
            "G2S00W7CTud8ZIEruzDydP-ik1PepymuSt8RCFIu52Bs4189U3Rz7CXJ3_A8iu4SOxqUDHVjb573RXrKwLZGqJUSDe1Bct_X",
            "kTwdJto-po5p4w_23dFyqVJ--fwX_H-LpoHtqUnmWc776RuvG_El3rvXUJEAt3y-c2t257Xim2iDx_eh5lEsgpwuErc9USct",
            "LDkgWjQN3HiIbT_WGY2DmMrSTZ-8ivGqR3mt9pC6lEqAWr08GIlfttBg8X7yQQFkosfk8O-jW0s-y-4rGAqkElRAwCQlru8E",
            "PpCsDGbJDwy7Wn0TLxy5kxF6gNwxVPxtZzHzUOuQy6aGvvNm6KqvXWxDJ-nNFyOZPDT8Et-xjyN9zTEkyfwdyaWf0Xybaj3P",
            "aIEZrfxMatZLzww3_DbcUnTunxdc6iYsliJCGpCLHUztLlnkXmFMbudyGyEB7GkUf80xZHChoBD6fx3WqTCC4FJbHntCagqn",
            "Nw1h9YEW5qnz8ujtVfyeKoJL0LiyZq-lXkP-ASAXxGAaniJQYMXw6qTTFVFWxuuhhndlFWMPqE5U-Dir5ayAH3xxxVlBRMX8",
            "sZXRqbA792oJtolR_e4uD8-Ya5CJh_HjGqnt4kR9uilVChCqlGejt_FjmvQhXivtizt7-zPaPQ4YvWMD6VM2XKgZxLOf207v",
            "txkTBKbulz_LeM55Y_Y9V6S_SJ5ShefOFicWV7eBUxMX2rrVbhhpbx5sz-A71U1Lia5nqcNJRG1xnYcNt_fhSgqTgGewSlFO",
            "SkYx6xFxUL1MZyequITsjOduMCv42qschTGw8i4rKcQGVFs-d_cFuWtCeaBLHkIygRXZwWQkkAPnCsRao7_De1iI_HoLla_h",
            "s6P7kWxvDUbqSn1FBijnIyNPKzq0uuqVrAoYNZ85ObgG97JWXq1s8T9o53qdrpuIq91lMS6vR2KuVvSKx2zleObuFlgvdCSp",
            "TuuZuhzB-wSlsltDdY9cz9DT3ICnPNJJur0tMhGxdrb4pbjaqB8P8NmOhgs3tqjVCMrwz7RdiPgL-ar2gl_ZbUtx_Zcx3Y_0",
            "Y9uNVuL02XPqRpr_wVqJvTZBWGHcpvdc0zuAQpye4JuuwpCQ14_LiXtE1vaXGpFf3OZcXE33hOc7yyoQ4Ms2iZQbQr6s0yXa",
            "gUkhvhvww414_ssOoJ053kDH0BJGAjdE4KPBYcvMkk2xt_j4SWPB2YhHiyiKVwU0mYUTXFwYHHmxui1PAvOuowyu84NMZzVI",
            "EHejZOpfes0F5hCWkCAKmDDvXYUVaNNI1DjbU7xAg1NKEyEl3nfStxmilRWCFAYXJAR9t5b457GX-6YlGSe3_09sM8lgDPUe",
            "SOMCWz_UgVUWtS04yVsi-uzUX5b5itKlx7wIxwnV3TQM6nzw3_M4VX31WSniJxqsZQkmqrG--_anBOWvUQnnePe0xHD4Qtya",
            "2Dk66fkvsVzdOuKLECPwd5UZnaK1RkyIp3E5Ek1pEuqUHb6zXdi6PpJDHPV8ar1qVfHc5G8hVORZ0B0meo95qfVPjd3jCkjk",
            "-rNg19e-_adWNODkes12EdqFhPKTYIkdRuD4XTAV4d6x1G-7EHARER6vHigtG8XEoxXtvkot_ZMfmBUZzCkOj6YYfO1JhN3P",
            "dZThqxdpahtvZ8msA5H3B8RSyTeQf_9ku7L9Dagut62aL4v8OPRZPJiseaql3v3PPRQ73fGoPhQY-IihBesh8McZc1duuekd",
            "ZkQ00H8PjC02uiz-94F7QZxf4uwq_3Pu1I6UZzWwExbLYywxUKsmAMYtzszagmCOmuz5aTExpD-jJPWc-yX1-g-xqTBZ8ie8",
            "SwAxeZcDcKa-kc78syXgF4sJPChPAhl7v7-s_EHNGUR2CYaOo-5l7vqzf_Xs6SJi7duVRn_6JcKgvOoouxiPIy_-Roo8cQoq",
            "jn_UhuaFD4J-v_6o21foHL1SCaMOnIlC_8Qno8I1F-9lVIEENjKNxeb73-xPUrrta7S6ifnsh59nigvfdWLZcSSv2qYVqDpD",
            "PU1bXut83fwuI0RORFH2Ktz3YUjqafzj7vkw6sAAG_QZ5HgDntj47HyNJcLtuA-AJJRlGqrtVtmsrTwwbna9yT2-_KHjTfcx",
            "rNgc4wUnGYVMTGFgpxSI_nHtRrxksP382-cSUqbgq_qBeyOkYjaEHr3NLPBNvQph1eTPRhvkH-xsZArlVKoj0KdvoqRrD-Od",
            "t03_3WrD36SvrCbgs_4dC5_ijZsYM_avcbal451coohrWCJDfpE7PdZEa69EAOrEeoKaK5a1klfMVA1A9ORJRWpGoYQPP93q",
            "zv8BOWnhteDRjSCGlbOYkCfsPVJDAMl6jF7rPQGklUXQEK5zbK_qNU6cD2e0G0Iph1jti7mQGipnF0XgvaKMx0IVHaDIbIJd",
            "7NL5jWyIdjA-ei71EoihwMyYKgQ5ya833Tay37Gco5reWhrSFFg_kdJbn51OxrUC66WaJcvz4fFrUrJkppcWwN4vGJVBbKwh",
            "_X-jquCsJR2TIKKGjF6_hphkfltpn0Nai7W93xIM_4xKTaUiL3gIqSOGNzVELKRIwIpPPNwoQan1eQb7F5yxB4BHCbeijMpF",
            "AjlWvo3QSPLlTfgSwAVkeaW0vVXnnICFvkObNIiWzeHbexLb",
        };

        static string _all;
        public static string All => _all ??= string.Concat(Parts);

        static Dictionary<string, int> _index;

        /// <summary>id -&gt; the level number that already is that cube.</summary>
        public static Dictionary<string, int> Index
        {
            get
            {
                if (_index != null) return _index;
                _index = new Dictionary<string, int>();
                string all = All;
                for (int i = 0; i + Width <= all.Length; i += Width)
                {
                    string id = all.Substring(i, Width);
                    if (!_index.ContainsKey(id)) _index[id] = i / Width + 1;
                }
                return _index;
            }
        }
    }
}
