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

using System;
using System.Collections.Generic;
using Saab.Foundation.Unity.MapStreamer;
using UnityEngine;

namespace Saab.MapStreamer.Benchmark
{
    public abstract class BenchmarkTest : MonoBehaviour
    {
        public virtual string TestName => GetType().Name;

        public virtual void SetUp(SceneManager sceneManager)
        {
        }

        public virtual void AddReportData(
            BenchmarkTestReport report,
            SceneManager sceneManager)
        {
        }

        public virtual void TearDown(SceneManager sceneManager)
        {
        }
    }

    [Serializable]
    public sealed class BenchmarkTestReport
    {
        public string name;
        public string type;
        public List<BenchmarkTestProperty> properties = new();
        public List<BenchmarkTestMetric> metrics = new();

        public void AddProperty(string propertyName, string value)
        {
            properties.Add(new BenchmarkTestProperty
            {
                name = propertyName,
                value = value,
            });
        }

        public void AddMetric(
            string metricName,
            double value,
            string unit = null)
        {
            metrics.Add(new BenchmarkTestMetric
            {
                name = metricName,
                value = value,
                unit = unit,
            });
        }
    }

    [Serializable]
    public sealed class BenchmarkTestProperty
    {
        public string name;
        public string value;
    }

    [Serializable]
    public sealed class BenchmarkTestMetric
    {
        public string name;
        public double value;
        public string unit;
    }
}
