using System.Collections.Generic;
using UnityEngine;
using Xlsx;
using FrameWork;
namespace Xlsx
{
	public class Xlsx_PlayerSkill
	{
		/// <summary>
		/// key
		/// </summary>
		public string Key;

		/// <summary>
		/// 技能名字
		/// </summary>
		public string SkillName;

		/// <summary>
		/// 伤害类型(0普通 1火焰 2冰霜)
		/// </summary>
		public int AtkType;

		/// <summary>
		/// 是否是百分百
		/// </summary>
		public int IsBFB;

		/// <summary>
		/// 等级数值
		/// </summary>
		public float[] LvValue;

		/// <summary>
		/// 等级CD
		/// </summary>
		public float[] LvCD;

		/// <summary>
		/// 等级数量
		/// </summary>
		public int[] LvCount;

		/// <summary>
		/// 时间
		/// </summary>
		public float[] LvTime;

		/// <summary>
		/// 其他数值
		/// </summary>
		public float[] LvOtherValue;

		/// <summary>
		/// 次数
		/// </summary>
		public int[] LvChiShu;

		/// <summary>
		/// 其他数值2
		/// </summary>
		public float[] LvOtherValue2;

		/// <summary>
		/// 技能默认描述
		/// </summary>
		public string SkillInfo;

		/// <summary>
		/// 技能升级描述
		/// </summary>
		public string UpInfo;

		/// <summary>
		/// 技能图标
		/// </summary>
		public string Icon;

		/// <summary>
		/// 属于职业
		/// </summary>
		public string RoleType;

		/// <summary>
		/// 法力消耗
		/// </summary>
		public float[] MpXh;

		/// <summary>
		/// 进入状态
		/// </summary>
		public string State;

		/// <summary>
		/// 技能范围
		/// </summary>
		public float Range;

		/// <summary>
		/// 是否是主动技能
		/// </summary>
		public int IsActiveSkill;

		/// <summary>
		/// 技能id
		/// </summary>
		public int SkillId;

		/// <summary>
		/// 后置技能
		/// </summary>
		public string[] SkillLUnLock;

		/// <summary>
		/// 技能前置解锁
		/// </summary>
		public string[] SkillFUnlick;

		/// <summary>
		/// 不能解锁的其他技能
		/// </summary>
		public string[] SkillNotUnlockOther;

		/// <summary>
		/// 伤害类型(1普通伤害 2火焰伤害 3冰霜伤害)
		/// </summary>
		public int HitType;

		/// <summary>
		/// 技能类型(1基础技能 2核心技能 3防御技能 4辅助技能 5奥义技能 6被动技能 7天赋)
		/// </summary>
		public int SkillType;

		public Xlsx_PlayerSkill(string Key,string SkillName,int AtkType,int IsBFB,float[] LvValue,float[] LvCD,int[] LvCount,float[] LvTime,float[] LvOtherValue,int[] LvChiShu,float[] LvOtherValue2,string SkillInfo,string UpInfo,string Icon,string RoleType,float[] MpXh,string State,float Range,int IsActiveSkill,int SkillId,string[] SkillLUnLock,string[] SkillFUnlick,string[] SkillNotUnlockOther,int HitType,int SkillType){
			this.Key=Key;
			this.SkillName=SkillName;
			this.AtkType=AtkType;
			this.IsBFB=IsBFB;
			this.LvValue=LvValue;
			this.LvCD=LvCD;
			this.LvCount=LvCount;
			this.LvTime=LvTime;
			this.LvOtherValue=LvOtherValue;
			this.LvChiShu=LvChiShu;
			this.LvOtherValue2=LvOtherValue2;
			this.SkillInfo=SkillInfo;
			this.UpInfo=UpInfo;
			this.Icon=Icon;
			this.RoleType=RoleType;
			this.MpXh=MpXh;
			this.State=State;
			this.Range=Range;
			this.IsActiveSkill=IsActiveSkill;
			this.SkillId=SkillId;
			this.SkillLUnLock=SkillLUnLock;
			this.SkillFUnlick=SkillFUnlick;
			this.SkillNotUnlockOther=SkillNotUnlockOther;
			this.HitType=HitType;
			this.SkillType=SkillType;

		}
		public Xlsx_PlayerSkill(){}	}
}
