/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

namespace SelfBalanceControl
{
	public class SlidingModeControl
	{
		public struct NominalModel
		{
			public MatrixXd A;	// 5x5 [v, w, theta_dot, theta, s]
			public MatrixXd B;	// 5x2
			public MatrixXd K;	// 2x5
			public MatrixXd S;	// 2x5

			public MatrixXd SxA => S * A;
			public MatrixXd SxB => S * B;
			public MatrixXd SxBxK => SxB * K;
		};

		public enum OutputMode
		{
			LQR,
			EQUIVALENT,
			SLIDING_MODE
		};

		public enum SwitchingMode
		{
			SAT,
			SIGN
		};

		private static readonly NominalModel _DefaultNominalModel = new NominalModel()
		{
			A = new MatrixXd(new double[,]
			{
				{0, 0, 0, -11.93722134, 0},
				{0, 0, 0, 0, 0},
				{0, 0, 0, 108.43889023, 0},
				{0, 0, 1, 0, 0},
				{1, 0, 0, 0, 0}
			}),
			B = new MatrixXd(new double[,]
			{
				{7.31329364, 7.31329364},
				{102.1006488, -102.1006488},
				{-36.46651836, -36.46651836},
				{0.0, 0.0},
				{0.0, 0.0}
			}),
			K = new MatrixXd(new double[,]
			{
				{-1.0966112 ,  0.70710678, -1.05096776, -6.36026143, -0.70710678},
				{-1.0966112 , -0.70710678, -1.05096776, -6.36026143, -0.70710678}
			}),
			S = new MatrixXd(new double[,]
			{
				{ 1, -1, 1, 1, 1 },
				{ 1,  1, 1, 1, 1 }
			})
		};

		private double _deltaTime;

		private Vector2d _uLQ = Vector2d.zero;
		private Vector2d _uEQ = Vector2d.zero;
		private Vector2d _uSW = Vector2d.zero;

		private VectorXd _f = VectorXd.Zero(5);

		private NominalModel _nominalModel = new NominalModel();

		private Vector2d _sigmaIntegral = Vector2d.zero;
		private Vector2d _sigmaIntegralElementPrev = Vector2d.zero;

		private double _kSW = 0.2; // switching gain
		private double _sigmaB = 0.001;
		// private double _sigmaIntegralLimit;
		private double _ff = 0; // Feed forward (?)

		private OutputMode _outputMode;
		private  SwitchingMode _switchingMode;

		public SlidingModeControl(in double deltaTime, in NominalModel model, OutputMode outMode, SwitchingMode switchMode)
		{
			this._deltaTime = deltaTime;
			this._outputMode = outMode;
			this._switchingMode = switchMode;
			this._nominalModel = model;

			SetDefault();
		}

		public SlidingModeControl(in double deltaTime, OutputMode outMode, SwitchingMode switchMode)
		: this(
				deltaTime,
				_DefaultNominalModel,
				outMode,
				switchMode)
		{
		}

		public SlidingModeControl(in double deltaTime)
			: this(
				deltaTime,
				_DefaultNominalModel,
				OutputMode.SLIDING_MODE,
				SwitchingMode.SAT)
		{
		}

		public void SetNominalModel(in string matrixA, in string matrixB, in string matrixK, in string matrixS)
		{
			var A = string.IsNullOrEmpty(matrixA) ? _DefaultNominalModel.A : new MatrixXd(matrixA);
			var B = string.IsNullOrEmpty(matrixB) ? _DefaultNominalModel.B : new MatrixXd(matrixB);
			var K = string.IsNullOrEmpty(matrixK) ? _DefaultNominalModel.K : new MatrixXd(matrixK);
			var S = string.IsNullOrEmpty(matrixS) ? _DefaultNominalModel.S : new MatrixXd(matrixS);
			// UnityEngine.Debug.Log(A.ToString("F15"));
			// UnityEngine.Debug.Log(B.ToString("F15"));
			// UnityEngine.Debug.Log(K.ToString("F15"));
			// UnityEngine.Debug.Log(S.ToString("F15"));

			this._nominalModel = new SlidingModeControl.NominalModel()
			{
				A = A,
				B = B,
				K = K,
				S = S
			};

			SetDefault();
		}

		public void SetDefault()
		{
			_sigmaIntegralElementPrev = Vector2d.zero;

			// recommanded values
			_nominalModel.S = _DefaultNominalModel.S;

			// this._kSW = 50;
			// this._sigmaB = 0.5;
			// // this._sigmaIntegralLimit = 100;
			// this._ff = 0.15;
		}

		public void Reset()
		{
			_sigmaIntegral = Vector2d.zero;
			_sigmaIntegralElementPrev = Vector2d.zero;
		}

		public void SetParams(in double K_sw, in double sigma_b, in double ff)
		{
			_kSW = K_sw;
			_sigmaB = sigma_b;
			_ff = ff;
		}

		public Vector2d ComputeControl(in VectorXd states, in VectorXd references, in double deltaTime)
		{
			var delta = states - references;
			var linearVelocity = states[0];

			_f = _nominalModel.B * (((linearVelocity >= 0) ? -_ff : _ff) * Vector2d.one);

			_uLQ = -_nominalModel.K * delta;
			_uEQ = _uLQ - (_nominalModel.SxB).Inverse * _nominalModel.S * _f;

			// UnityEngine.Debug.Log($"states: {states} | references: {references} | Delta: {delta}");
			// UnityEngine.Debug.Log($"K: {_nominalModel.K} | f: {_f} | uLQ({_uLQ}) | uEQ({_uEQ})");
			// UnityEngine.Debug.Log($"uLQ({_uLQ}) | K: {_nominalModel.K} | Delta: {delta}");

			var sigmaIntegralElement = (_nominalModel.SxA - _nominalModel.SxBxK) * delta;
			// UnityEngine.Debug.Log($"SA: {_nominalModel.SxA} SBK: {_nominalModel.SxBxK}");
			// UnityEngine.Debug.Log($"SA-SBK: {_nominalModel.SxA - _nominalModel.SxBxK}  | Delta: {delta}");
			// UnityEngine.Debug.Log($"sigmaintegralelem: {sigmaIntegralElement}");

			for (var i = 0; i < sigmaIntegralElement.Size; i++)
			{
				_sigmaIntegral[i] += 0.5 * (sigmaIntegralElement[i] + _sigmaIntegralElementPrev[i]) * deltaTime;
			}

			// UnityEngine.Debug.Log($"_sigmaIntegral: {_sigmaIntegral}");
			var sigma = _nominalModel.S * delta - _sigmaIntegral;
			_sigmaIntegralElementPrev = sigmaIntegralElement;

			_uSW = Vector2d.zero;
			switch (_switchingMode)
			{
				case SwitchingMode.SAT:
					_uSW = (_nominalModel.SxB).Inverse * _kSW * SAT(sigma, _sigmaB);
					break;
				case SwitchingMode.SIGN:
					_uSW = (_nominalModel.SxB).Inverse * _kSW * Sign(sigma);
					break;
				default:
					throw new InvalidOperationException("Unknown switching mode.");
			}

			var u = Vector2d.zero;
			switch (_outputMode)
			{
				case OutputMode.LQR:
					u = _uLQ;
					break;
				case OutputMode.EQUIVALENT:
					u = _uEQ;
					break;
				case OutputMode.SLIDING_MODE:
					u = _uEQ - _uSW;
					break;
				default:
					throw new InvalidOperationException("Unknown output mode.");
			}

			// UnityEngine.Debug.Log($"sigma({sigma}) | sigmaB({_sigmaB})");
			// UnityEngine.Debug.Log($"uEQ({_uEQ}) | uSW({_uSW}) | u({u})");
			return u;
		}

		private Vector2d Sign(in Vector2d sigma)
		{
			var res = Vector2d.zero;
			for (var i = 0; i < 2; i++)
			{
				res[i] = (sigma[i] > 0) ? 1 : -1;
			}
			return res;
		}

		private Vector2d SAT(Vector2d sigma, in double _sigmaB)
		{
			var res = Vector2d.zero;
			for (var i = 0; i < 2; i++)
			{
				if (Math.Abs(sigma[i]) < _sigmaB)
				{
					res[i] = sigma[i] / _sigmaB;
				}
				else
				{
					res[i] = (sigma[i] > 0) ? 1 : -1;
				}
			}
			return res;
		}
	}}
