﻿using System.Collections.Generic;

namespace ICE.Ui
{
    /// <summary>
    /// IDs of missions that should be disabled and shown as unsupported.
    /// </summary>
    public static class UnsupportedMissions
    {
        public static readonly HashSet<uint> Ids = new HashSet<uint>
        {
            0, // blacklisted mission ID
            512, 513, 514, // CRP
            515, 516, 517, // BSM
            518, 519, 520, // ARM
            521, 522, 523, // GSM
            524, 525, 526, // LTW
            527, 528, 529, // WVR
            530, 531, 532, // ALC
            533, 534, 535, // CUL

            366, 367, 368, 369, 370, 371, 372,
            373, 374, 375, 376, 377, 378, 379,
            380, 381, 382, 383, 384, 385, 386,
            387, 388, 389, 390, 391, 392, 393,
            394, 395, 396, 397, 398, 399, 400,
            401, 402, 403, 404, 405, // MIN

            406, 407, 408, 409, 410, 411, 
            412, 413, 414, 415, 416, 417, 418, 
            419, 420, 421, 422, 423, 424, 425, 426, 
            427, 428, 429, 430, 431, 432, 433, 434, 435, 436, 437,
            438, 439, 440, 441, 442, 443, 444, 445,
            446, 447, 448, 449, 450, // BTN


            451, 452, 453, 454, 455, 456, 457, 458,
            459, 460, 461, 462, 463, 464, 465, 466,
            467, 468, 469, 470, 471, 472, 473, 474,
            475, 476, 477, 478, 479, 480, 481, 482,
            483, 484, 485, 486, 487, 488, 489, 490,
            491, 492, 493, 494, 495, // FSH
        };
    }
}
