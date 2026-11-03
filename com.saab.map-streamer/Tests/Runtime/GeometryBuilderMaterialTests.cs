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

using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Saab.Foundation.Unity.MapStreamer.Tests
{
    public sealed class GeometryBuilderMaterialTests
    {
        private GameObject _gameObject;
        private Material _stateMaterial;
        private Material _fallbackMaterial;
        private Texture2D _texture;
        private TerrainNodeBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find("Standard");
            _stateMaterial = new Material(shader) { name = "State" };
            _fallbackMaterial = new Material(shader) { name = "Fallback" };
            _texture = new Texture2D(1, 1);
            _gameObject = new GameObject(nameof(GeometryBuilderMaterialTests));
            _builder = _gameObject.AddComponent<TerrainNodeBuilder>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(GeometryBuilderBase).GetField("_material", flags).SetValue(_builder, _stateMaterial);
            typeof(GeometryBuilderBase).GetField("_fallbackMaterial", flags).SetValue(_builder, _fallbackMaterial);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
            Object.DestroyImmediate(_texture);
            Object.DestroyImmediate(_stateMaterial);
            Object.DestroyImmediate(_fallbackMaterial);
        }

        [Test]
        public void PlaceholderTextureUsesFallbackMaterialWithUniformColor()
        {
            var color = new Color(1f, .5f, .1f, 1f);
            var material = _builder.CreateSceneMaterial(
                new StateBuildOutput { Texture = _texture, IsPlaceholderTexture = true }, color);
            try
            {
                Assert.That(material.name, Does.StartWith("Fallback"));
                Assert.That(material.mainTexture, Is.Null);
                var actual = material.color;
                Assert.That(actual.r, Is.EqualTo(color.r).Within(1e-3f));
                Assert.That(actual.g, Is.EqualTo(color.g).Within(1e-3f));
                Assert.That(actual.b, Is.EqualTo(color.b).Within(1e-3f));
                Assert.That(actual.a, Is.EqualTo(color.a).Within(1e-3f));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void StateTextureUsesStateMaterial()
        {
            var material = _builder.CreateSceneMaterial(
                new StateBuildOutput { Texture = _texture }, Color.white);
            try
            {
                Assert.That(material.name, Does.StartWith("State"));
                Assert.That(material.mainTexture, Is.SameAs(_texture));
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void PlaceholderAndMissingTexturesUseFallbackMaterial()
        {
            Assert.That(_builder.UsesFallbackMaterial(default), Is.True);
            Assert.That(_builder.UsesFallbackMaterial(
                new StateBuildOutput { Texture = _texture, IsPlaceholderTexture = true }), Is.True);
            Assert.That(_builder.UsesFallbackMaterial(new StateBuildOutput { Texture = _texture }), Is.False);
        }

        [Test]
        public void UniformVertexColorIsReturned()
        {
            var color = new Color(1f, .3f, .05f, 1f);
            var mesh = CreateTriangle(color, color, color);
            try
            {
                Assert.That(GeometryHelper.TryGetUniformVertexColor(mesh, out var actual), Is.True);
                Assert.That(actual.r, Is.EqualTo(color.r).Within(1e-3f));
                Assert.That(actual.g, Is.EqualTo(color.g).Within(1e-3f));
                Assert.That(actual.b, Is.EqualTo(color.b).Within(1e-3f));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void MixedOrMissingVertexColorsAreNotUniform()
        {
            var mixed = CreateTriangle(Color.red, Color.red, Color.blue);
            var uncolored = CreateTriangle();
            try
            {
                Assert.That(GeometryHelper.TryGetUniformVertexColor(mixed, out var mixedColor), Is.False);
                Assert.That(mixedColor, Is.EqualTo(Color.white));
                Assert.That(GeometryHelper.TryGetUniformVertexColor(uncolored, out _), Is.False);
                Assert.That(GeometryHelper.TryGetUniformVertexColor(null, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(mixed);
                Object.DestroyImmediate(uncolored);
            }
        }

        private static Mesh CreateTriangle(params Color[] colors)
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } };
            if (colors.Length > 0)
                mesh.colors = colors;
            mesh.triangles = new[] { 0, 1, 2 };
            return mesh;
        }
    }
}
